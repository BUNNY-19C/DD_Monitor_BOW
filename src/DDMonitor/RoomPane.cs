using Microsoft.Web.WebView2.WinForms;

namespace DDMonitor;

internal sealed class RoomPane : Panel
{
    private readonly BrowserSession session;
    private readonly string roomId;
    private readonly Action<RoomPane> openPage;
    private readonly Action<RoomPane> selectAudio;
    private readonly WebView2 browser = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Black };
    private readonly Label caption;
    private readonly Button pageButton;
    private readonly Button sound;
    private readonly ContextMenuStrip menu = Ui.Menu();
    private readonly System.Windows.Forms.Timer monitor = new() { Interval = 10000 };
    private bool reading;
    private bool initialized;
    private bool audioReady;
    private bool compact = true;
    private bool listening;
    private string? displayName;
    private int dragEdges;
    private Point dragStart;
    private Rectangle dragBounds;
    private bool dragging;

    public RoomPane(string roomId, BrowserSession session, Action<RoomPane> openPage, Action<RoomPane> selectAudio, Action<string, string> titleChanged)
    {
        this.roomId = roomId;
        this.session = session;
        this.openPage = openPage;
        this.selectAudio = selectAudio;
        Dock = DockStyle.None;
        BackColor = Ui.Surface;
        Padding = new Padding(3);
        caption = Ui.Label(roomId + " · 加载中", 9);
        var bar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 32, BackColor = Ui.Surface, ColumnCount = 4, RowCount = 1, Padding = new Padding(6, 1, 4, 1) };
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
        caption.Dock = DockStyle.Fill;
        caption.Cursor = Cursors.SizeAll;
        caption.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) BeginAdjust(0); };
        bar.Controls.Add(caption, 0, 0);
        pageButton = Ui.Button("网页", (_, _) => this.openPage(this));
        pageButton.Dock = DockStyle.Fill;
        bar.Controls.Add(pageButton, 1, 0);
        sound = Ui.Button("静音", (_, _) => SetListening(!listening));
        sound.Dock = DockStyle.Fill;
        bar.Controls.Add(sound, 2, 0);
        menu.Items.Add("完整网页 / 返回视频", null, (_, _) => this.openPage(this));
        menu.Items.Add("切换声音", null, (_, _) => SetListening(!listening));
        menu.Items.Add("刷新直播间", null, (_, _) => { if (initialized) browser.Reload(); });
        var enlarge = menu.Items.Add("放大此窗口", null, (_, _) => ResizeWindow(new Size((int)(Width * 1.25), (int)(Height * 1.25))));
        var shrink = menu.Items.Add("缩小此窗口", null, (_, _) => ResizeWindow(new Size((int)(Width * 0.8), (int)(Height * 0.8))));
        menu.Opening += (_, _) => { enlarge.Enabled = shrink.Enabled = Parent is RoomWorkspace workspace && workspace.CanAdjust; };
        var more = Ui.Button("更多", (_, _) => menu.Show(bar, new Point(Math.Max(0, bar.Width - 180), bar.Height)));
        more.Dock = DockStyle.Fill;
        bar.Controls.Add(more, 3, 0);
        foreach (Button button in new[] { pageButton, sound, more })
        {
            button.Font = Ui.SmallFont;
            button.Margin = new Padding(2, 1, 2, 1);
            button.Padding = Padding.Empty;
        }
        bar.SizeChanged += (_, _) =>
        {
            bool wide = bar.ClientSize.Width >= 320 * bar.DeviceDpi / 96;
            pageButton.Visible = wide;
            bar.ColumnStyles[1].Width = wide ? 64 * bar.DeviceDpi / 96F : 0;
        };
        browser.CoreWebView2InitializationCompleted += (_, e) =>
        {
            if (!e.IsSuccess || IsDisposed) return;
            browser.CoreWebView2.DocumentTitleChanged += (_, _) =>
            {
                string[] parts = browser.CoreWebView2.DocumentTitle.Split(" - ");
                if (parts.Length < 3) return;
                displayName = parts[^2].Trim();
                caption.Text = displayName + " · " + roomId;
                titleChanged(roomId, displayName);
            };
        };
        Controls.Add(browser);
        Controls.Add(bar);
        var grip = new Panel { Size = new Size(16, 16), BackColor = Ui.Surface, Cursor = Cursors.SizeNWSE, AccessibleName = "拖动调整此直播窗口大小" };
        grip.Paint += (_, e) =>
        {
            using var pen = new Pen(Ui.Muted);
            for (int i = 4; i <= 12; i += 4) e.Graphics.DrawLine(pen, grip.Width - i, grip.Height - 2, grip.Width - 2, grip.Height - i);
        };
        grip.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) BeginAdjust(2 | 8); };
        Controls.Add(grip);
        Resize += (_, _) => { grip.Location = new Point(Width - grip.Width, Height - grip.Height); grip.Visible = Parent is RoomWorkspace workspace && workspace.CanAdjust; grip.BringToFront(); };
        monitor.Tick += async (_, _) => await ReadPlaybackAsync();
    }

    public void ResizeWindow(Size size)
    {
        if (Parent is RoomWorkspace workspace) workspace.ResizeRoom(this, size);
    }

    private int Edges(Point point)
    {
        int edge = Math.Max(5, 5 * DeviceDpi / 96);
        return (point.X < edge ? 1 : point.X >= Width - edge ? 2 : 0) |
            (point.Y < edge ? 4 : point.Y >= Height - edge ? 8 : 0);
    }

    private void BeginAdjust(int edges)
    {
        if (Parent is not RoomWorkspace workspace || !workspace.CanAdjust) return;
        dragEdges = edges;
        dragStart = workspace.PointToClient(MousePosition);
        dragBounds = Bounds;
        dragging = true;
        Capture = true;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int edges = Edges(e.Location);
        if (e.Button == MouseButtons.Left && edges != 0) BeginAdjust(edges);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Parent is not RoomWorkspace workspace || !workspace.CanAdjust) return;
        if (!dragging)
        {
            int edges = Edges(e.Location);
            Cursor = edges is 3 or 1 or 2 ? Cursors.SizeWE : edges is 4 or 8 ? Cursors.SizeNS : edges is 5 or 10 ? Cursors.SizeNWSE : edges is 6 or 9 ? Cursors.SizeNESW : Cursors.Default;
            return;
        }
        Point current = workspace.PointToClient(PointToScreen(e.Location));
        int dx = current.X - dragStart.X, dy = current.Y - dragStart.Y;
        Rectangle next = dragBounds;
        if (dragEdges == 0)
        {
            next.Offset(dx, dy);
            workspace.PreviewMove(this, new Point(next.Left + next.Width / 2, next.Top + next.Height / 2));
            return;
        }
        else
        {
            if ((dragEdges & 1) != 0) { next.X += dx; next.Width -= dx; }
            if ((dragEdges & 2) != 0) next.Width += dx;
            if ((dragEdges & 4) != 0) { next.Y += dy; next.Height -= dy; }
            if ((dragEdges & 8) != 0) next.Height += dy;
            int minWidth = 180 * DeviceDpi / 96, minHeight = 140 * DeviceDpi / 96;
            if (next.Width < minWidth) { next.Width = minWidth; if ((dragEdges & 1) != 0) next.X = dragBounds.Right - minWidth; }
            if (next.Height < minHeight) { next.Height = minHeight; if ((dragEdges & 4) != 0) next.Y = dragBounds.Bottom - minHeight; }
        }
        workspace.Adjust(this, next);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (dragging && dragEdges == 0 && Parent is RoomWorkspace workspace) workspace.CommitMove(this);
        dragging = false;
        Capture = false;
    }
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture) { dragging = false; if (Parent is RoomWorkspace workspace) workspace.CancelMove(); }
    }

    public void SetListening(bool value)
    {
        listening = value;
        sound.Text = value ? "声音" : "静音";
        sound.AccessibleDescription = value ? "当前房间出声" : "当前房间已静音";
        sound.BackColor = value ? Ui.Primary : Ui.Control;
        sound.ForeColor = value ? Ui.PrimaryText : Ui.Text;
        sound.FlatAppearance.MouseOverBackColor = value ? Ui.PrimaryHover : Ui.Border;
        if (value) selectAudio(this);
        _ = ApplyMuteAsync();
    }

    private async Task ApplyMuteAsync()
    {
        if (!initialized || IsDisposed) return;
        bool muted = !listening || !audioReady;
        browser.CoreWebView2.IsMuted = muted;
        try { await browser.ExecuteScriptAsync("window.__ddAudio?.setMuted(" + (muted ? "true" : "false") + ")"); }
        catch (Exception error) { AppLog.Error("Audio selection update failed", error); }
    }

    public async Task SetCompactAsync(bool value)
    {
        compact = value;
        pageButton.Text = value ? "网页" : "视频";
        if (!initialized || IsDisposed) return;
        browser.ZoomFactor = value ? 1.0 : 0.85;
        try { await browser.ExecuteScriptAsync("window.__ddView?.setCompact(" + (value ? "true" : "false") + ")"); }
        catch (Exception error) { AppLog.Error("Viewing mode change failed", error); }
    }

    public async Task InitializeAsync()
    {
        try
        {
            await session.InitializeAsync(browser);
            if (IsDisposed) return;
            initialized = true;
            browser.CoreWebView2.IsMuted = true;
            await browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(PlayerScripts.Compact);
            browser.CoreWebView2.NavigationStarting += async (_, _) =>
            {
                audioReady = false;
                await ApplyMuteAsync();
            };
            browser.CoreWebView2.WebMessageReceived += async (_, e) =>
            {
                if (IsDisposed || !Uri.TryCreate(e.Source, UriKind.Absolute, out var source) || source.Host != "live.bilibili.com") return;
                if (!e.WebMessageAsJson.Contains("dd-audio-ready")) return;
                await session.ApplyVolumeAsync(browser);
                if (IsDisposed) return;
                audioReady = true;
                await ApplyMuteAsync();
            };
            browser.CoreWebView2.NavigationCompleted += async (_, args) =>
            {
                if (IsDisposed) return;
                caption.Text = args.IsSuccess ? roomId : roomId + " · 加载失败";
                if (args.IsSuccess) { await SetCompactAsync(compact); await ReadPlaybackAsync(); }
            };
            browser.ZoomFactor = 1.0;
            browser.CoreWebView2.Navigate("https://live.bilibili.com/" + roomId);
            monitor.Start();
        }
        catch (Exception error)
        {
            if (!IsDisposed) caption.Text = roomId + " · 初始化失败";
            AppLog.Error("Room browser initialization failed", error);
        }
    }

    private async Task ReadPlaybackAsync()
    {
        if (IsDisposed || !initialized || reading) return;
        reading = true;
        try
        {
            string result = await browser.ExecuteScriptAsync("(() => {const v=document.querySelector('video');return !!v&&!v.paused&&!v.ended&&v.readyState>=2;})()");
            if (IsDisposed) return;
            bool playing = result == "true";
            string title = displayName == null ? roomId : displayName + " · " + roomId;
            caption.Text = playing ? title : title + " · 等待播放";
        }
        catch (Exception error)
        {
            if (!IsDisposed) caption.Text = roomId + " · 请检查网页";
            AppLog.Error("Playback status unavailable", error);
        }
        finally { reading = false; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { monitor.Stop(); monitor.Dispose(); menu.Dispose(); browser.Dispose(); }
        base.Dispose(disposing);
    }
}
