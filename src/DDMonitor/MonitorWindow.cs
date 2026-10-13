using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;

namespace DDMonitor;

internal sealed class MonitorWindow : Form
{
    private readonly BrowserSession session;
    private readonly string statePath;
    private readonly WebView2 loginBrowser = new() { Dock = DockStyle.Fill };
    private readonly WebView2 accountBrowser = new() { Size = new Size(1, 1), Location = new Point(-10, -10), TabStop = false };
    private readonly Panel loginPanel = new() { Dock = DockStyle.Fill };
    private readonly Panel workspace = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel sidebar = new() { Dock = DockStyle.Left, Width = 300, BackColor = Ui.Surface };
    private readonly Panel viewers = new() { Dock = DockStyle.Fill, BackColor = Ui.Background };
    private readonly RoomWorkspace grid = new() { Dock = DockStyle.Fill, BackColor = Ui.Background, Visible = false, Padding = new Padding(2) };
    private readonly VolumeSlider globalVolume = new() { Dock = DockStyle.Fill };
    private readonly Label volumeLabel = Ui.Label("音量 15%", 9);
    private readonly System.Windows.Forms.Timer volumeDelay = new() { Interval = 120 };
    private Button backToGrid = null!;
    private RoomPane? focusedRoom;
    private bool pendingWatch;
    private readonly bool startMuted;
    private readonly Panel empty = new() { Dock = DockStyle.Fill };
    private readonly Label accountLabel = Ui.Label("正在检查登录状态", 9, color: Ui.Muted);
    private readonly Label loginStatus = Ui.Label("正在加载 B 站官方登录页", color: Ui.Warning);
    private readonly Label notice = Ui.Label("", color: Ui.Muted);
    private readonly TextBox roomInput = new() { Font = Ui.BodyFont, PlaceholderText = "房号或 B 站直播间链接", BackColor = Ui.Header, ForeColor = Ui.Text, BorderStyle = BorderStyle.None };
    private readonly RoomLibrary roomList = new();
    private readonly Button layoutChoice = Ui.Button("自动布局", (_, _) => { });
    private readonly ContextMenuStrip layoutMenu = Ui.Menu();
    private int layoutColumns;
    private readonly List<RoomPane> rooms = [];
    private readonly System.Windows.Forms.Timer poll = new() { Interval = 8000 };
    private readonly Button watchButton;
    private readonly Button logoutButton;
    private readonly SemaphoreSlim accountRequests = new(1, 1);
    private readonly RoomCovers covers = new();
    private readonly CancellationTokenSource lifetime = new();
    private Account? account;
    private bool initialized;
    private bool checking;
    private bool closing;
    private int generation;

    public MonitorWindow(string root, string[]? initialRooms = null, bool startMuted = false)
    {
        Text = "DD监控室 · 登录与观看";
        BackColor = Ui.Background;
        ForeColor = Ui.Text;
        Font = Ui.BodyFont;
        Size = new Size(1540, 980);
        MinimumSize = new Size(1080, 720);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        HandleCreated += (_, _) => Ui.DarkTitleBar(this);
        statePath = Path.Combine(root, ".runtime", "workspace.json");
        session = new BrowserSession(Path.Combine(root, ".runtime", "web-profile"), WorkspaceStore.LoadVolume(statePath));
        globalVolume.Value = session.VolumePercent;
        pendingWatch = initialRooms?.Length > 0;
        this.startMuted = startMuted;
        watchButton = Ui.Button("开始观看", async (_, _) => await StartWatchingAsync(), true);
        watchButton.Enabled = false;
        logoutButton = Ui.Button("退出登录", async (_, _) => await LogoutAsync());
        logoutButton.Visible = false;
        BuildHeader();
        BuildLogin();
        BuildWorkspace();
        grid.LayoutEdited += (_, _) => { layoutChoice.Text = "联动布局"; notice.Text = "拖动边框联动缩放邻窗，拖动标题交换位置；顶部布局菜单可重新排列"; };
        Controls.Add(workspace);
        Controls.Add(loginPanel);
        Controls.Add(accountBrowser);
        Controls.SetChildIndex(workspace, 2);
        Controls.SetChildIndex(loginPanel, 1);
        foreach (string room in WorkspaceStore.Load(statePath)) roomList.Items.Add(room);
        foreach (string initialRoom in initialRooms ?? [])
        {
            int index = roomList.Items.IndexOf(initialRoom);
            if (index < 0) { roomList.Items.Add(initialRoom); index = roomList.Items.Count - 1; }
            roomList.SetItemChecked(index, true);
        }
        volumeDelay.Tick += async (_, _) =>
        {
            volumeDelay.Stop();
            await session.SetVolumeAsync(globalVolume.Value);
            SaveRooms();
        };
        poll.Tick += async (_, _) => await VerifyAccountAsync();
        Shown += async (_, _) => await InitializeAsync();
        FormClosing += (_, _) =>
        {
            closing = true;
            lifetime.Cancel();
            covers.Dispose();
            generation++;
            poll.Stop();
            StopWatching();
            session.ClosePopups();
            SaveRooms();
            loginBrowser.Dispose();
            accountBrowser.Dispose();
            poll.Dispose();
            volumeDelay.Dispose();
            layoutMenu.Dispose();
        };
    }

    private void BuildHeader()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 88, BackColor = Ui.Header, ColumnCount = 1, RowCount = 2, Padding = new Padding(16, 4, 16, 6) };
        // 原版平面设计，无阴影
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var identity = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Ui.Header, ColumnCount = 4, RowCount = 1, Margin = new Padding(0) };
        identity.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int width in new[] { 240, 90, 90 }) identity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
        var brand = Ui.Label("DD 监控室", 18, true);
        brand.Dock = DockStyle.Fill;
        identity.Controls.Add(brand, 0, 0);
        accountLabel.Dock = logoutButton.Dock = DockStyle.Fill;
        accountLabel.TextAlign = ContentAlignment.MiddleRight;
        accountLabel.Margin = new Padding(0, 0, 18, 0);
        identity.Controls.Add(accountLabel, 1, 0);
        var check = Ui.Button("检查登录", async (_, _) => await VerifyAccountAsync());
        check.Dock = DockStyle.Fill;
        check.Margin = logoutButton.Margin = new Padding(3, 4, 3, 4);
        identity.Controls.Add(check, 2, 0);
        identity.Controls.Add(logoutButton, 3, 0);
        header.Controls.Add(identity, 0, 0);
        var navigation = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Ui.Header, ColumnCount = 5, RowCount = 1, Margin = new Padding(0) };
        navigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        var toggle = Ui.Button("直播库", (_, _) => { if (account != null) sidebar.Visible = !sidebar.Visible; });
        toggle.Dock = DockStyle.Fill;
        toggle.Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold);
        toggle.ForeColor = Ui.Accent;
        toggle.BackColor = Ui.Header;
        toggle.FlatAppearance.BorderSize = 0;
        toggle.Paint += (_, e) => { using var line = new Pen(Ui.Accent, 2 * toggle.DeviceDpi / 96F); e.Graphics.DrawLine(line, 9, toggle.Height - 2, toggle.Width - 9, toggle.Height - 2); };
        navigation.Controls.Add(toggle, 0, 0);
        backToGrid = Ui.Button("返回多窗口", async (_, _) => await ReturnToGridAsync());
        backToGrid.Dock = DockStyle.Fill;
        backToGrid.Visible = false;
        navigation.Controls.Add(backToGrid, 1, 0);
        layoutChoice.Dock = DockStyle.Fill;
        layoutChoice.Margin = new Padding(8, 4, 8, 4);
        layoutChoice.AccessibleName = "视频排列列数";
        for (int i = 0; i <= 4; i++)
        {
            int columns = i;
            string label = i == 0 ? "自动布局" : i + " 列";
            layoutMenu.Items.Add(label, null, async (_, _) =>
            {
                layoutColumns = columns;
                layoutChoice.Text = label;
                if (rooms.Count == 0) return;
                await ReturnToGridAsync();
                ConfigureGrid(rooms.Count);
                notice.Text = "布局已更新";
            });
        }
        layoutChoice.Click += (_, _) => layoutMenu.Show(layoutChoice, new Point(0, layoutChoice.Height));
        navigation.Controls.Add(layoutChoice, 3, 0);
        var audio = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Ui.Header, Margin = new Padding(10, 0, 0, 0) };
        audio.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        audio.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        audio.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        volumeLabel.Dock = DockStyle.Fill;
        volumeLabel.Text = $"音量 {globalVolume.Value}%";
        audio.Controls.Add(volumeLabel, 0, 0);
        audio.Controls.Add(globalVolume, 1, 0);
        globalVolume.ValueChanged += (_, _) =>
        {
            volumeLabel.Text = $"音量 {globalVolume.Value}%";
            volumeDelay.Stop();
            volumeDelay.Start();
        };
        navigation.Controls.Add(audio, 4, 0);
        header.Controls.Add(navigation, 0, 1);
        Controls.Add(header);
    }

    private void BuildLogin()
    {
        loginPanel.Padding = new Padding(32);
        var introduction = new Panel { Dock = DockStyle.Left, Width = 325, Padding = new Padding(0, 20, 30, 0) };
        var title = Ui.Label("先登录，\n再开始观看", 23, true);
        title.SetBounds(0, 20, 295, 110);
        var description = Ui.Label("使用哔哩哔哩 App 扫码，\n或在右侧官方页面登录。\n\n登录后，直播页、弹幕和观看任务\n会使用同一个账号。", color: Ui.Muted);
        description.SetBounds(0, 150, 295, 150);
        loginStatus.SetBounds(0, 325, 295, 70);
        var verify = Ui.Button("我已登录，检查状态", async (_, _) => await VerifyAccountAsync(), true);
        verify.SetBounds(0, 415, 285, 42);
        var reload = Ui.Button("重新加载登录页", (_, _) => ReloadLogin());
        reload.SetBounds(0, 470, 285, 42);
        var note = Ui.Label("观看时长与亲密度，以直播间\n粉丝勋章任务面板的记录为准。", color: Ui.Muted);
        note.SetBounds(0, 565, 295, 75);
        introduction.Controls.AddRange([title, description, loginStatus, verify, reload, note]);
        loginPanel.Controls.Add(loginBrowser);
        loginPanel.Controls.Add(introduction);
    }

    private void BuildWorkspace()
    {
        var column = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(14, 14, 14, 12), BackColor = Ui.Surface };
        foreach (float height in new[] { 38F, 40F, 42F }) column.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        column.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (float height in new[] { 38F, 42F, 38F }) column.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        var title = Ui.Label("我的直播间", 11, true);
        title.Dock = DockStyle.Fill;
        column.Controls.Add(title, 0, 0);
        roomInput.Dock = DockStyle.Fill;
        roomInput.PlaceholderText = "房号或链接，回车观看";
        roomInput.AccessibleName = "直播间房号或链接";
        roomInput.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            if (AddRoom()) await StartWatchingAsync();
        };
        var inputBox = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Header, Padding = new Padding(8, 6, 8, 4), Margin = new Padding(3) };
        inputBox.Paint += (_, e) =>
        {
            float scale = inputBox.DeviceDpi / 96F;
            // 边框 - 简洁实线，聚焦变蓝（原版风格）
            using var line = new Pen(roomInput.Focused ? Ui.Accent : Ui.Border, 1 * scale);
            e.Graphics.DrawRectangle(line, 0, 0, inputBox.Width - 1, inputBox.Height - 1);
            // 去掉发光效果 - 原版没有，减少干扰
        };
        roomInput.Enter += (_, _) => inputBox.Invalidate();
        roomInput.Leave += (_, _) => inputBox.Invalidate();
        inputBox.Controls.Add(roomInput);
        column.Controls.Add(inputBox, 0, 1);
        var add = Ui.Button("添加并观看", async (_, _) => { if (AddRoom()) await StartWatchingAsync(); });
        add.Dock = DockStyle.Fill;
        column.Controls.Add(add, 0, 2);
        roomList.Dock = DockStyle.Fill;
        roomList.AccessibleName = "勾选要一起观看的直播间";
        column.Controls.Add(roomList, 0, 3);
        var options = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Ui.Surface, Margin = new Padding(0) };
        options.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        var remove = Ui.Button("移除", (_, _) => { if (roomList.SelectedIndex >= 0) { roomList.RemoveRoom(roomList.SelectedIndex); SaveRooms(); } });
        remove.Dock = DockStyle.Fill;
        var selectionHint = Ui.Label("勾选后一起观看", 8.5F, color: Ui.Muted);
        selectionHint.Dock = DockStyle.Fill;
        options.Controls.Add(selectionHint, 0, 0);
        options.Controls.Add(remove, 1, 0);
        column.Controls.Add(options, 0, 4);
        watchButton.Text = "观看勾选房间";
        watchButton.Dock = DockStyle.Fill;
        column.Controls.Add(watchButton, 0, 5);
        var stop = Ui.Button("停止观看", (_, _) => StopWatching());
        stop.Dock = DockStyle.Fill;
        column.Controls.Add(stop, 0, 6);
        sidebar.Controls.Add(column);
        var emptyLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(45) };
        emptyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        emptyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        emptyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));
        emptyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var emptyTitle = Ui.Label("开始观看", 20, true);
        emptyTitle.Dock = DockStyle.Fill;
        emptyLayout.Controls.Add(emptyTitle, 0, 1);
        var copy = Ui.Label("输入房号或链接，回车即可打开。\n\n视频格里的「网页」可打开弹幕和勋章任务。", color: Ui.Muted);
        copy.Dock = DockStyle.Fill;
        emptyLayout.Controls.Add(copy, 0, 2);
        empty.Controls.Add(emptyLayout);
        viewers.Controls.Add(empty);
        viewers.Controls.Add(grid);
        workspace.Controls.Add(viewers);
        workspace.Controls.Add(sidebar);
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = Ui.Header, Padding = new Padding(16, 1, 16, 1) };
        notice.Dock = DockStyle.Fill;
        notice.Font = new Font("Microsoft YaHei UI", 9F);
        footer.Controls.Add(notice);
        Controls.Add(footer);
    }

    private async Task InitializeAsync()
    {
        try
        {
            await session.InitializeAsync(loginBrowser);
            await session.InitializeAsync(accountBrowser);
            if (closing) return;
            initialized = true;
            loginBrowser.ZoomFactor = 0.9;
            loginBrowser.CoreWebView2.NavigationCompleted += async (_, _) =>
            {
                if (closing || account != null || loginBrowser.Source?.Scheme != "https") return;
                loginStatus.Text = "请在右侧完成官方登录";
                await VerifyAccountAsync();
            };
            ReloadLogin();
            await VerifyAccountAsync();
            poll.Start();
            AppLog.Info("Edge desktop initialized; authentication gate active");
        }
        catch (Exception error)
        {
            loginStatus.Text = "浏览器初始化失败，请确认 WebView2 Runtime 已安装";
            AppLog.Error("Browser initialization failed", error);
        }
    }

    private void ReloadLogin()
    {
        if (!initialized || closing) return;
        loginStatus.Text = "正在加载官方登录页";
        loginBrowser.CoreWebView2.Navigate(BrowserSession.LoginUrl);
    }

    private async Task<bool> VerifyAccountAsync()
    {
        if (!initialized || closing || checking) return false;
        checking = true;
        watchButton.Enabled = false;
        int current = generation;
        await accountRequests.WaitAsync();
        try
        {
            if (closing) return false;
            if (!await BrowserSession.NavigateAsync(accountBrowser, BrowserSession.NavUrl))
                throw new IOException("Account verification timed out");
            string encoded = await accountBrowser.ExecuteScriptAsync("document.body.innerText");
            string text = JsonSerializer.Deserialize<string>(encoded) ?? "";
            if (closing || current != generation) return false;
            using JsonDocument document = JsonDocument.Parse(text);
            int code = document.RootElement.GetProperty("code").GetInt32();
            if (code == -101) { SetAccount(null); return false; }
            Account? confirmed = Account.FromNav(text);
            if (confirmed == null) throw new JsonException("Unverified account response");
            SetAccount(confirmed);
            return true;
        }
        catch (Exception error)
        {
            if (!closing && current == generation)
            {
                loginStatus.Text = "暂时无法验证登录，请完成网页验证后点击「检查登录」";
                notice.Text = "账号验证失败，请检查网络后重试";
                watchButton.Enabled = false;
                AppLog.Error("Account verification unavailable", error);
            }
            return false;
        }
        finally { accountRequests.Release(); checking = false; }
    }

    private void SetAccount(Account? confirmed)
    {
        Account? previous = account;
        account = confirmed;
        if (confirmed == null)
        {
            if (previous != null) { StopWatching(); ReloadLogin(); loginStatus.Text = "登录已失效，请重新登录后继续观看"; }
            accountLabel.Text = "未登录";
            watchButton.Enabled = false;
            logoutButton.Visible = false;
            workspace.Visible = false;
            loginPanel.Visible = true;
            loginPanel.BringToFront();
            poll.Interval = 8000;
            return;
        }
        if (previous != null && previous.Uid != confirmed.Uid) StopWatching();
        accountLabel.Text = confirmed.Name + " · 已登录";
        logoutButton.Visible = true;
        watchButton.Enabled = true;
        loginPanel.Visible = false;
        workspace.Visible = true;
        workspace.BringToFront();
        poll.Interval = 60000;
        if (previous == null)
        {
            loginBrowser.CoreWebView2.Navigate("about:blank");
            notice.Text = "登录已验证，可以选择直播间";
            AppLog.Info("Server confirmed login; room selection unlocked");
            _ = RefreshLibraryAsync(roomList.Items.Cast<string>().ToArray());
            if (pendingWatch)
            {
                pendingWatch = false;
                BeginInvoke(new Action(async () => await StartWatchingAsync()));
            }
        }
    }

    private async Task RefreshLibraryAsync(IEnumerable<string> ids)
    {
        foreach (string id in ids)
        {
            if (closing || account == null) return;
            RoomInfo? info = null;
            try
            {
                await accountRequests.WaitAsync();
                try
                {
                    if (closing || account == null) return;
                    if (!await BrowserSession.NavigateAsync(accountBrowser, "https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom?room_id=" + id))
                        throw new IOException("Room information timed out");
                    string text = JsonSerializer.Deserialize<string>(await accountBrowser.ExecuteScriptAsync("document.body.innerText")) ?? "";
                    info = RoomInfo.Parse(text);
                }
                finally { accountRequests.Release(); }
                Image? cover = null;
                try { cover = await covers.LoadAsync(info.Cover, lifetime.Token); }
                catch (Exception error) when (error is HttpRequestException or TaskCanceledException or ArgumentException)
                { if (!closing) AppLog.Error("Room cover unavailable", error); }
                if (closing) { cover?.Dispose(); return; }
                roomList.SetRoomInfo(id, info, cover);
            }
            catch (Exception error) when (error is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
            {
                if (!closing) { notice.Text = $"房间 {id} 资料加载失败，可移除后重新添加"; AppLog.Error("Room information unavailable", error); }
            }
        }
    }

    private async Task LogoutAsync()
    {
        if (!initialized || closing) return;
        generation++;
        poll.Stop();
        logoutButton.Enabled = false;
        StopWatching();
        session.ClosePopups();
        try
        {
            await loginBrowser.CoreWebView2.Profile.ClearBrowsingDataAsync(
                Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.AllSite);
            SetAccount(null);
            ReloadLogin();
            loginStatus.Text = "已退出此应用登录，请重新扫码";
        }
        catch (Exception error)
        {
            notice.Text = "退出失败，请检查网络后重试";
            AppLog.Error("Sign out failed", error);
        }
        finally { logoutButton.Enabled = true; if (!closing) poll.Start(); }
    }

    private bool AddRoom()
    {
        if (account == null) return false;
        try
        {
            string id = RoomId.Parse(roomInput.Text);
            int existing = roomList.Items.IndexOf(id);
            if (existing >= 0)
            {
                roomList.SelectedIndex = existing;
                roomList.SetItemChecked(existing, true);
                return true;
            }
            roomList.Items.Add(id);
            int index = roomList.Items.Count - 1;
            roomList.SetItemChecked(index, true);
            roomInput.Clear();
            notice.Text = $"已添加直播间 {id}";
            SaveRooms();
            _ = RefreshLibraryAsync(new[] { id });
            return true;
        }
        catch (FormatException error) { notice.Text = error.Message; return false; }
    }

    private async Task StartWatchingAsync()
    {
        if (account == null) { notice.Text = "请先完成 B 站登录"; return; }
        string[] selected = roomList.CheckedItems.Cast<string>().ToArray();

        if (selected.Length == 0) { notice.Text = "先勾选至少一个直播间"; return; }
        if (selected.Length > 16) { notice.Text = "最多同时观看 16 个房间"; return; }
        if (!await VerifyAccountAsync() || closing) return;
        StopWatching();
        int current = generation;
        ConfigureGrid(selected.Length);
        empty.Visible = false;
        grid.Visible = true;
        foreach ((string id, int index) in selected.Select((id, index) => (id, index)))
        {
            if (closing || current != generation) return;
            var pane = new RoomPane(id, session, p => { _ = OpenPageAsync(p); }, SelectAudio, roomList.SetRoomName);
            rooms.Add(pane);
            grid.Controls.Add(pane);
            grid.ArrangeRooms();
            pane.SetListening(index == 0 && !startMuted);
            await pane.InitializeAsync();
        }
        notice.Text = "拖动边框联动缩放邻窗，拖动标题交换位置；「网页」中可查看勋章任务";
    }

    private int ConfigureGrid(int count)
    {
        int columns = Math.Min(count, layoutColumns == 0 ? (int)Math.Ceiling(Math.Sqrt(count)) : layoutColumns);
        grid.Arrange(rooms, columns);
        return columns;
    }

    private void StopWatching()
    {
        generation++;
        RestoreGrid();
        foreach (RoomPane pane in rooms.ToArray()) { grid.Controls.Remove(pane); pane.Dispose(); }
        rooms.Clear();
        grid.Visible = false;
        empty.Visible = true;
        notice.Text = "已停止观看";
    }


    private void SelectAudio(RoomPane selected)
    {
        foreach (RoomPane pane in rooms) if (pane != selected) pane.SetListening(false);
    }

    private async Task OpenPageAsync(RoomPane pane)
    {
        if (focusedRoom == pane) { await ReturnToGridAsync(); return; }
        if (focusedRoom != null) await ReturnToGridAsync();
        focusedRoom = pane;
        grid.FocusRoom(pane);
        backToGrid.Visible = true;
        await pane.SetCompactAsync(false);
        notice.Text = "完整网页：可发送弹幕、查看粉丝勋章任务；点击「返回多窗口」继续监看";
    }

    private void RestoreGrid()
    {
        if (focusedRoom == null) return;
        grid.RestoreRooms();
        focusedRoom = null;
        backToGrid.Visible = false;
    }

    private async Task ReturnToGridAsync()
    {
        RoomPane? previous = focusedRoom;
        RestoreGrid();
        if (previous != null) await previous.SetCompactAsync(true);
        notice.Text = "视频视图；点击「声音」可切换当前出声的直播间";
    }

    private void SaveRooms()
    {
        try { WorkspaceStore.Save(statePath, roomList.Items.Cast<string>(), globalVolume.Value); }
        catch (IOException error) { notice.Text = "直播间列表无法保存，请检查目录权限"; AppLog.Error("Preferences write failed", error); }
    }
}
