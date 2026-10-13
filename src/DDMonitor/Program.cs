namespace DDMonitor;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string root = FindRoot();
        AppLog.Path = System.IO.Path.Combine(root, ".runtime", "desktop.log");
        Application.ThreadException += (_, e) => AppLog.Error("UI exception", e.Exception);
        string[] initialRooms = args.Select((argument, index) => (argument, index))
            .Where(x => x.argument == "--room" && x.index + 1 < args.Length)
            .Select(x => RoomId.Parse(args[x.index + 1])).Distinct().ToArray();
        using var window = new MonitorWindow(root, initialRooms, args.Contains("--muted"));
        Application.Run(window);
        return 0;
    }

    private static string FindRoot()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (DirectoryInfo? dir = new(start); dir != null; dir = dir.Parent)
                if (File.Exists(System.IO.Path.Combine(dir.FullName, "src", "DDMonitor", "DDMonitor.csproj")))
                    return dir.FullName;
        }
        throw new DirectoryNotFoundException("请从 DD_Monitor 项目目录启动程序");
    }
}

internal static class AppLog
{
    public static string Path = "desktop.log";
    public static void Info(string message)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.AppendAllText(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch (IOException) { }
    }
    public static void Error(string context, Exception error) => Info($"{context}: {error.GetType().Name}");
}
