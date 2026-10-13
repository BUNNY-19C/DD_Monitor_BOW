using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DDMonitor;

internal static class Ui
{
    // 主色调 - 吸收原DD监控室的实用主义
    public static readonly Color Background = Color.FromArgb(27, 30, 37);      // 深色背景，不抢内容
    public static readonly Color Header = Color.FromArgb(23, 26, 33);          // 更深的头部，建立层次
    public static readonly Color Surface = Color.FromArgb(35, 40, 49);         // 卡片表面
    public static readonly Color Control = Color.FromArgb(49, 54, 59);         // #31363b - 继承原版灰色按钮
    public static readonly Color ControlHover = Color.FromArgb(58, 67, 81);
    public static readonly Color Border = Color.FromArgb(65, 77, 94);

    // 文字颜色 - 高对比度保证可读性
    public static readonly Color Text = Color.FromArgb(241, 254, 251);         // #f1fefb - 原版主文字色
    public static readonly Color Muted = Color.FromArgb(170, 182, 196);        // 次要文字
    public static readonly Color VeryMuted = Color.FromArgb(140, 150, 160);    // 最次要信息

    // 强调色 - 原版蓝色 #3daee9 作为激活状态
    public static readonly Color Accent = Color.FromArgb(61, 174, 233);        // #3daee9 - 原版激活蓝
    public static readonly Color Primary = Color.FromArgb(61, 174, 233);       // 统一使用原版蓝
    public static readonly Color PrimaryHover = Color.FromArgb(80, 188, 240);
    public static readonly Color PrimaryText = Header;

    // 状态色 - 简洁直观
    public static readonly Color Warning = Color.FromArgb(246, 199, 107);
    public static readonly Color LiveIndicator = Color.FromArgb(255, 92, 92);  // 红点 - 原版直播指示器

    // 字体
    public static readonly Font BodyFont = new("Microsoft YaHei UI", 10F);
    public static readonly Font SmallFont = new("Microsoft YaHei UI", 8.5F);

    public static Label Label(string text, float size = 10, bool bold = false, Color? color = null)
        => new() { Text = text, ForeColor = color ?? Text, BackColor = Color.Transparent,
            Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };

    public static Button Button(string text, EventHandler clicked, bool primary = false)
    {
        var button = new Button { Text = text, FlatStyle = FlatStyle.Flat,
            ForeColor = primary ? PrimaryText : Text,
            BackColor = primary ? Primary : Control,
            Font = BodyFont, Height = 40, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        button.FlatAppearance.BorderSize = 1;  // 统一边框，简化视觉
        button.FlatAppearance.BorderColor = primary ? Primary : Border;
        button.FlatAppearance.MouseOverBackColor = primary ? PrimaryHover : ControlHover;
        button.Click += clicked;
        return button;
    }

    public static void DarkTitleBar(Form form)
    {
        int enabled = 1;
        DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int));
    }

    public static ContextMenuStrip Menu() => new()
    {
        Font = BodyFont, ForeColor = Text, BackColor = Surface,
        ShowImageMargin = false, Renderer = new ToolStripProfessionalRenderer(new MenuColors())
    };

    private sealed class MenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Border;
        public override Color MenuItemSelected => Control;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}

// Small native control: avoids the system TrackBar's bright channel in a dark app.
internal sealed class VolumeSlider : System.Windows.Forms.Control
{
    private int value = 15;
    public event EventHandler? ValueChanged;
    public int Value
    {
        get => value;
        set
        {
            int next = Math.Clamp(value, 0, 100);
            if (this.value == next) return;
            this.value = next;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public VolumeSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        BackColor = Ui.Header;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleName = "全局音量";
        AccessibleRole = AccessibleRole.Slider;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float inset = 8 * DeviceDpi / 96F;
        float width = Math.Max(1, ClientSize.Width - inset * 2);
        float middle = ClientSize.Height / 2F;
        float thumb = inset + width * Value / 100F;
        float trackWidth = 4 * DeviceDpi / 96F;

        // 绘制轨道（灰色部分）
        using var track = new Pen(Ui.Control, trackWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawLine(track, inset, middle, inset + width, middle);

        // 绘制已填充部分（蓝色）
        if (Value > 0)
        {
            using var fill = new Pen(Ui.Accent, trackWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLine(fill, inset, middle, thumb, middle);
        }

        // 绘制滑块（圆点）
        float knobSize = 12 * DeviceDpi / 96F;
        using var knob = new SolidBrush(Ui.Text);
        using var knobBorder = new Pen(Ui.Accent, 2 * DeviceDpi / 96F);
        e.Graphics.FillEllipse(knob, thumb - knobSize / 2, middle - knobSize / 2, knobSize, knobSize);
        e.Graphics.DrawEllipse(knobBorder, thumb - knobSize / 2, middle - knobSize / 2, knobSize, knobSize);

        // 焦点框
        if (Focused)
        {
            using var focusPen = new Pen(Ui.Accent, 1 * DeviceDpi / 96F) { DashStyle = DashStyle.Dot };
            var focusRect = new Rectangle(1, 1, ClientRectangle.Width - 2, ClientRectangle.Height - 2);
            e.Graphics.DrawRectangle(focusPen, focusRect);
        }
    }

    private void ChangeFromPointer(int x)
    {
        float inset = 8 * DeviceDpi / 96F;
        Value = (int)Math.Round((x - inset) / Math.Max(1, Width - inset * 2) * 100);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        Capture = true;
        ChangeFromPointer(e.X);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Capture) ChangeFromPointer(e.X);
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture = false; }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Left: case Keys.Down: Value -= 5; break;
            case Keys.Right: case Keys.Up: Value += 5; break;
            case Keys.Home: Value = 0; break;
            case Keys.End: Value = 100; break;
            default: return;
        }
        e.Handled = true;
        e.SuppressKeyPress = true;
    }
}
