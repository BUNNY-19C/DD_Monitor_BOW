namespace DDMonitor;

internal sealed class RoomLibrary : Control
{
    public List<string> Items { get; } = [];
    private int selectedIndex = -1;
    private int offset;
    private bool scrolling;
    private int scrollGrab;
    private int ItemHeight => 100 * DeviceDpi / 96;  // 恢复原版紧凑密度，工具性优先
    private int MaxScroll => Math.Max(0, Items.Count * ItemHeight - ClientSize.Height);
    public int SelectedIndex
    {
        get => selectedIndex;
        set { selectedIndex = Math.Clamp(value, -1, Items.Count - 1); EnsureVisible(); Invalidate(); }
    }
    private readonly HashSet<string> checkedRooms = [];
    private readonly Dictionary<string, string> names = [];
    private readonly Dictionary<string, RoomCard> cards = [];
    private readonly ToolTip tooltip = new();
    private int hovered = -1;
    private sealed record RoomCard(RoomInfo Info, Image? Cover);
    public IEnumerable<string> CheckedItems => Items.Cast<string>().Where(checkedRooms.Contains);

    public RoomLibrary()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Ui.Surface;
        ForeColor = Ui.Text;
        Font = Ui.BodyFont;
        AccessibleName = "选择一起观看的直播间；空格切换勾选";
    }

    public void SetItemChecked(int index, bool value)
    {
        string room = (string)Items[index];
        if (value) checkedRooms.Add(room); else checkedRooms.Remove(room);
        Invalidate();
    }

    public void SetRoomName(string room, string name) { names[room] = name; Invalidate(); }

    public void SetRoomInfo(string room, RoomInfo info, Image? cover)
    {
        if (!Items.Contains(room)) { cover?.Dispose(); return; }
        if (cards.Remove(room, out var previous)) previous.Cover?.Dispose();
        cards[room] = new RoomCard(info, cover);
        names[room] = info.Anchor;
        Invalidate();
    }

    public void RemoveRoom(int index)
    {
        string room = (string)Items[index];
        if (cards.Remove(room, out var card)) card.Cover?.Dispose();
        names.Remove(room);
        checkedRooms.Remove(room);
        Items.RemoveAt(index);
        selectedIndex = Math.Min(selectedIndex, Items.Count - 1);
        offset = Math.Min(offset, MaxScroll);
        Invalidate();
    }

    private int IndexFromPoint(Point point)
    {
        if (!ClientRectangle.Contains(point)) return -1;
        int index = (point.Y + offset) / ItemHeight;
        return index < Items.Count ? index : -1;
    }

    private Rectangle ScrollThumb
    {
        get
        {
            int height = Math.Min(Height, Math.Max(24 * DeviceDpi / 96, Height * Height / Math.Max(1, Items.Count * ItemHeight)));
            return new Rectangle(Width - 8 * DeviceDpi / 96, MaxScroll == 0 ? 0 : (Height - height) * offset / MaxScroll, 5 * DeviceDpi / 96, height);
        }
    }

    private void EnsureVisible()
    {
        if (selectedIndex < 0) return;
        int top = selectedIndex * ItemHeight;
        if (top < offset) offset = top;
        else if (top + ItemHeight > offset + Height) offset = top + ItemHeight - Height;
        offset = Math.Clamp(offset, 0, MaxScroll);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        if (MaxScroll > 0 && e.X >= Width - 12 * DeviceDpi / 96)
        {
            scrolling = true;
            scrollGrab = ScrollThumb.Contains(e.Location) ? e.Y - ScrollThumb.Top : ScrollThumb.Height / 2;
            Capture = true;
            ScrollToPointer(e.Y);
            return;
        }
        int index = IndexFromPoint(e.Location);
        if (e.Button == MouseButtons.Left && index >= 0)
        {
            string room = (string)Items[index];
            SelectedIndex = index;
            SetItemChecked(index, !checkedRooms.Contains(room));
        }
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Up or Keys.Down or Keys.Home or Keys.End)
        {
            SelectedIndex = e.KeyCode == Keys.Home ? 0 : e.KeyCode == Keys.End ? Items.Count - 1 : Math.Clamp(SelectedIndex + (e.KeyCode == Keys.Up ? -1 : 1), 0, Math.Max(0, Items.Count - 1));
            e.Handled = e.SuppressKeyPress = true;
            return;
        }
        if (e.KeyCode != Keys.Space || SelectedIndex < 0) return;
        string room = (string)Items[SelectedIndex];
        SetItemChecked(SelectedIndex, !checkedRooms.Contains(room));
        e.Handled = e.SuppressKeyPress = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (scrolling) { ScrollToPointer(e.Y); return; }
        int index = IndexFromPoint(e.Location);
        if (index == hovered) return;
        hovered = index;
        string text = index >= 0 && cards.TryGetValue((string)Items[index], out var card)
            ? card.Info.Title + "\n" + card.Info.Anchor + " · " + Items[index] : "";
        tooltip.SetToolTip(this, text);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hovered == -1) return;
        hovered = -1;
        tooltip.SetToolTip(this, "");
        Invalidate();
    }

    private void ScrollToPointer(int y)
    {
        offset = Math.Clamp((int)((y - scrollGrab) * (double)MaxScroll / Math.Max(1, Height - ScrollThumb.Height)), 0, MaxScroll);
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); scrolling = false; Capture = false; }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) scrolling = false; }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        offset = Math.Clamp(offset - e.Delta / 120 * 48 * DeviceDpi / 96, 0, MaxScroll);
        Invalidate();
    }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnResize(EventArgs e) { base.OnResize(e); offset = Math.Min(offset, MaxScroll); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        for (int i = offset / ItemHeight; i < Items.Count && i * ItemHeight - offset < Height; i++)
        {
            DrawItemState state = i == SelectedIndex ? DrawItemState.Selected | (Focused ? DrawItemState.Focus : DrawItemState.None) : DrawItemState.None;
            DrawRoomItem(new DrawItemEventArgs(e.Graphics, Font, new Rectangle(0, i * ItemHeight - offset, Width - (MaxScroll > 0 ? 12 * DeviceDpi / 96 : 0), ItemHeight), i, state));
        }
        if (MaxScroll > 0) { using var thumb = new SolidBrush(Ui.Muted); e.Graphics.FillRectangle(thumb, ScrollThumb); }
    }
    private void DrawRoomItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        string room = (string)Items[e.Index];
        bool chosen = checkedRooms.Contains(room);
        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool isHovered = e.Index == hovered;

        // 背景 - 选中或悬停时高亮
        Color bgColor = chosen ? Ui.Control : (selected || isHovered ? Ui.ControlHover : Ui.Surface);
        using var background = new SolidBrush(bgColor);
        e.Graphics.FillRectangle(background, e.Bounds);

        float scale = DeviceDpi / 96F;
        int padding = (int)(8 * scale);

        // 绘制勾选框 - 更大更明显
        int checkSize = (int)(18 * scale);
        var check = new Rectangle(e.Bounds.X + (int)(12 * scale), e.Bounds.Y + (e.Bounds.Height - checkSize) / 2, checkSize, checkSize);

        if (chosen)
        {
            // 选中：填充蓝色背景
            using var checkBg = new SolidBrush(Ui.Accent);
            e.Graphics.FillRectangle(checkBg, check);
            using var checkBorder = new Pen(Ui.Accent, 1.5F * scale);
            e.Graphics.DrawRectangle(checkBorder, check);

            // 白色对勾
            using var tick = new Pen(Color.White, 2 * scale) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            Point[] checkmark = new Point[]
            {
                new(check.Left + (int)(4 * scale), check.Top + check.Height / 2),
                new(check.Left + check.Width / 2 - (int)(1 * scale), check.Bottom - (int)(5 * scale)),
                new(check.Right - (int)(4 * scale), check.Top + (int)(5 * scale))
            };
            e.Graphics.DrawLines(tick, checkmark);
        }
        else
        {
            // 未选中：空框
            using var line = new Pen(Ui.Border, 1.5F * scale);
            e.Graphics.DrawRectangle(line, check);
        }

        // 封面区域
        int coverWidth = (int)(98 * scale);
        int coverHeight = (int)(55 * scale);
        var coverArea = new Rectangle(check.Right + (int)(12 * scale), e.Bounds.Top + (e.Bounds.Height - coverHeight) / 2, coverWidth, coverHeight);
        bool hasCard = cards.TryGetValue(room, out var card);

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if (card?.Cover != null)
        {
            // 直角封面（原版风格）
            e.Graphics.DrawImage(card.Cover, coverArea);
        }
        else
        {
            // 占位符
            using var empty = new SolidBrush(Ui.Header);
            e.Graphics.FillRectangle(empty, coverArea);
            TextRenderer.DrawText(e.Graphics, hasCard ? "暂无封面" : "加载中", Ui.SmallFont, coverArea, Ui.VeryMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;

        // 文字区域
        int left = coverArea.Right + (int)(12 * scale);
        int rightMargin = (int)(12 * scale);
        int availableWidth = Math.Max(1, e.Bounds.Right - left - rightMargin);

        var titleArea = new Rectangle(left, e.Bounds.Top + (int)(10 * scale), availableWidth, (int)(40 * scale));
        var detailArea = new Rectangle(left, titleArea.Bottom + (int)(2 * scale), availableWidth, (int)(16 * scale));
        var statusArea = new Rectangle(left, detailArea.Bottom + (int)(2 * scale), availableWidth, (int)(16 * scale));

        // 标题
        bool hasName = names.TryGetValue(room, out string? name);
        string title = card?.Info.Title ?? name ?? room;
        Color titleColor = chosen ? Ui.Accent : Ui.Text;
        TextRenderer.DrawText(e.Graphics, title, Ui.BodyFont, titleArea, titleColor, TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);

        // 主播名 · 房号
        string detail = hasName ? name + " · " + room : "房间 " + room;
        TextRenderer.DrawText(e.Graphics, detail, Ui.SmallFont, detailArea, Ui.Muted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        // 状态标签 - 红点表示直播（原版设计）
        if (hasCard)
        {
            bool isLive = card!.Info.IsLive;
            string status = isLive ? "● 直播中" : "○ 未开播";
            Color statusColor = isLive ? Ui.LiveIndicator : Ui.VeryMuted;  // 红点 - 直观醒目
            TextRenderer.DrawText(e.Graphics, status, Ui.SmallFont, statusArea, statusColor, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
        else
        {
            TextRenderer.DrawText(e.Graphics, "加载房间信息...", Ui.SmallFont, statusArea, Ui.VeryMuted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        // 焦点框 - 简洁实线
        if ((e.State & DrawItemState.Focus) != 0)
        {
            using var focusPen = new Pen(Ui.Accent, 2 * scale);  // 2px 蓝色实线，原版风格
            var focusRect = new Rectangle(e.Bounds.X + 1, e.Bounds.Y + 1, e.Bounds.Width - 3, e.Bounds.Height - 3);
            e.Graphics.DrawRectangle(focusPen, focusRect);
        }

        // 去掉分隔线 - 原版没有，列表更干净
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { foreach (RoomCard card in cards.Values) card.Cover?.Dispose(); tooltip.Dispose(); }
        base.Dispose(disposing);
    }
}
