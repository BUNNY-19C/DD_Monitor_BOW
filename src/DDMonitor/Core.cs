using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DDMonitor;

internal sealed record Account(long Uid, string Name)
{
    public static Account? FromNav(string text)
    {
        using JsonDocument document = JsonDocument.Parse(text);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.Number ||
            !code.TryGetInt32(out int status) || status != 0 ||
            !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("isLogin", out var login) || login.ValueKind != JsonValueKind.True ||
            !data.TryGetProperty("mid", out var mid) || mid.ValueKind != JsonValueKind.Number ||
            !mid.TryGetInt64(out long uid) || uid <= 0)
            return null;
        string name = data.TryGetProperty("uname", out var user) && user.ValueKind == JsonValueKind.String
            ? user.GetString() ?? uid.ToString() : uid.ToString();
        return new Account(uid, name);
    }
}

internal static class RoomId
{
    public static string Parse(string input)
    {
        string value = input.Trim();
        if (!Regex.IsMatch(value, "^[0-9]+$"))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
                uri.Scheme != "https" || uri.Host != "live.bilibili.com" ||
                uri.UserInfo.Length != 0 || !uri.IsDefaultPort ||
                !Regex.IsMatch(uri.AbsolutePath, "^/[0-9]+/?$"))
                throw new FormatException("请输入房号或 https://live.bilibili.com/房号");
            value = uri.AbsolutePath.Trim('/');
        }
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long id) || id <= 0)
            throw new FormatException("房号必须是有效的正整数");
        return id.ToString(CultureInfo.InvariantCulture);
    }
}

internal static class WorkspaceStore
{
    public static List<string> Load(string state)
    {
        if (!File.Exists(state)) return [];
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(state));
            IEnumerable<string> ids = document.RootElement.GetProperty("rooms").EnumerateArray().Select(x => x.GetString() ?? "");
            return ids.Select(RoomId.Parse).Distinct().ToList();
        }
        catch (Exception e) when (e is IOException or JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            AppLog.Error("Room preferences could not be read", e);
            return [];
        }
    }

    public static int LoadVolume(string path)
    {
        try
        {
            using var data = JsonDocument.Parse(File.ReadAllText(path));
            if (data.RootElement.TryGetProperty("volume", out var volume) && volume.TryGetInt32(out int value))
                return Math.Clamp(value, 0, 100);
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException) { }
        return 15;
    }

    public static void Save(string path, IEnumerable<string> rooms, int volume = 15)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new { rooms = rooms.ToArray(), volume = Math.Clamp(volume, 0, 100) }));
        File.Move(temporary, path, true);
    }
}
