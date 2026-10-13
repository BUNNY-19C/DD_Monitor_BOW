namespace DDMonitor.Tests;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        if (CoreTests.Run() != 0) return 1;
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try { NativeControlTests.Run(); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
