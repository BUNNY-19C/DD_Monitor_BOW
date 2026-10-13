namespace DDMonitor;

internal static class CoreTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks++;
        }
        try
        {
            Check(Account.FromNav("{\"code\":-101,\"data\":{\"isLogin\":false}}") == null, "Logged-out accounts must remain locked");
            Check(Account.FromNav("{\"code\":0,\"data\":{\"isLogin\":\"true\",\"mid\":1}}") == null, "A string is not server-confirmed login");
            Check(Account.FromNav("{\"code\":0,\"data\":{\"isLogin\":true}}") == null, "Missing UID must not unlock viewing");
            Check(Account.FromNav("{\"code\":0,\"data\":[]}") == null, "Malformed account data must remain locked");
            Check(Account.FromNav("{\"code\":0,\"data\":{\"isLogin\":true,\"mid\":123,\"uname\":\"测试\"}}") == new Account(123, "测试"), "Positive account response should unlock viewing");
            Check(RoomId.Parse(" 00123 ") == "123", "Canonicalize numeric room IDs");
            Check(RoomId.Parse("https://live.bilibili.com/21919321?from=share") == "21919321", "Accept official sharing URLs");
            var info = RoomInfo.Parse("""{"code":0,"data":{"room_info":{"title":"直播标题","cover":"https://i0.hdslb.com/cover.jpg","live_status":1},"anchor_info":{"base_info":{"uname":"主播"}}}}""");
            Check(info.Title == "直播标题" && info.Anchor == "主播" && info.IsLive, "Read stream title independently of the anchor name");
            Check(RoomInfo.IsCoverUrl(info.Cover), "Accept official cover CDN");
            foreach (string url in new[] { "http://i0.hdslb.com/cover.jpg", "https://127.0.0.1/a.jpg", "https://192.168.1.1/a.jpg", "https://i0.hdslb.com.evil.test/a.jpg", "https://evil@i0.hdslb.com/a.jpg" })
                Check(!RoomInfo.IsCoverUrl(url), "Reject unsupported cover address");
            Check(RoomWorkspace.ResizePair([300, 300], 0, 100, 180).SequenceEqual(new[] { 400, 200 }), "Growing a room must shrink its neighbor without losing space");
            Check(RoomWorkspace.ResizePair([300, 300], 0, 500, 180).SequenceEqual(new[] { 420, 180 }), "Stop growth at the neighboring room's minimum width");
            Check(RoomWorkspace.ResizePair([300, 300], 0, -500, 180).SequenceEqual(new[] { 180, 420 }), "Keep the resized room above its minimum width");
            Check(RoomWorkspace.ResizePair([300, 300, 300], 0, 100, 180).SequenceEqual(new[] { 400, 200, 300 }), "Resizing a boundary must leave unrelated columns intact");
            Check(RoomWorkspace.ResizePair([300, 300], 1, 100, 180).SequenceEqual(new[] { 300, 300 }), "The outer workspace edge must stay fixed");
            Check(RoomWorkspace.ResizePair([120, 120], 0, 100, 180).SequenceEqual(new[] { 120, 120 }), "Small workspaces must not reduce already undersized rooms");
            Check(RoomWorkspace.AllocateTracks(1000, [1, 1], 2).SequenceEqual(new[] { 499, 499 }), "Fill the complete workspace except for the shared gap");
            Check(RoomWorkspace.AllocateTracks(1000, [598, 398], 2).SequenceEqual(new[] { 599, 399 }), "Retain edited room sizes during layout");
            Check(RoomWorkspace.AllocateTracks(1200, [598, 398], 2).Sum() == 1198, "Fill the workspace after resizing the main window");
            foreach (string value in new[] { "0", "-1", "https://localhost/123", "https://live.bilibili.com.evil.test/123", "https://live.bilibili.com@127.0.0.1/123", "https://live.bilibili.com:7897/123", "javascript:alert(1)", "你好" })
            {
                bool rejected = false;
                try { RoomId.Parse(value); } catch (FormatException) { rejected = true; }
                Check(rejected, "Reject non-room input: " + value);
            }
            string temp = Path.Combine(Path.GetTempPath(), "DDMonitor-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                string state = Path.Combine(temp, "workspace.json");
                Check(WorkspaceStore.Load(state).Count == 0, "Missing preferences start with an empty room list");
                WorkspaceStore.Save(state, new[] { "789" });
                Check(WorkspaceStore.Load(state).SequenceEqual(new[] { "789" }), "Save and restore new room selections");
                Check(WorkspaceStore.LoadVolume(state) == 15, "Start with quiet default volume");
                WorkspaceStore.Save(state, new[] { "789" }, 7);
                Check(WorkspaceStore.LoadVolume(state) == 7, "Remember the user's volume setting");
                Check(!File.ReadAllText(state).Contains("SESSDATA"), "Preferences must not copy authentication secrets");
            }
            finally { Directory.Delete(temp, true); }
            Console.WriteLine($"PASS: {checks} account, room-info, input, preferences and window-bounds checks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }
}
