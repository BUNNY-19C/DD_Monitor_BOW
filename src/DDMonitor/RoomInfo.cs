using System.Net.Http;
using System.Text.Json;

namespace DDMonitor;

internal sealed record RoomInfo(string Title, string Anchor, string Cover, bool IsLive)
{
    public static RoomInfo Parse(string json)
    {
        using var data = JsonDocument.Parse(json);
        JsonElement root = data.RootElement;
        if (root.GetProperty("code").GetInt32() != 0) throw new IOException("Room information unavailable");
        JsonElement details = root.GetProperty("data"), room = details.GetProperty("room_info");
        string cover = room.GetProperty("cover").GetString() ?? "";
        if (cover.Length == 0 && room.TryGetProperty("keyframe", out var frame)) cover = frame.GetString() ?? "";
        return new RoomInfo(room.GetProperty("title").GetString() ?? "", details.GetProperty("anchor_info").GetProperty("base_info").GetProperty("uname").GetString() ?? "", cover,
            room.TryGetProperty("live_status", out var status) && status.GetInt32() == 1);
    }

    public static bool IsCoverUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == "https" && uri.UserInfo.Length == 0 && uri.IsDefaultPort &&
        (uri.Host.EndsWith(".hdslb.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".biliimg.com", StringComparison.OrdinalIgnoreCase));
}

internal sealed class RoomCovers : IDisposable
{
    private readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
    public RoomCovers()
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
        client.DefaultRequestHeaders.Referrer = new Uri("https://live.bilibili.com/");
    }
    public async Task<Image?> LoadAsync(string url, CancellationToken cancellation)
    {
        if (!RoomInfo.IsCoverUrl(url)) return null;
        byte[] bytes = await client.GetByteArrayAsync(url, cancellation);
        using var stream = new MemoryStream(bytes);
        using var image = Image.FromStream(stream);
        return new Bitmap(image, new Size(320, 180));
    }
    public void Dispose() => client.Dispose();
}
