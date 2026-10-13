using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DDMonitor;

internal sealed class BrowserSession
{
    public const string LoginUrl = "https://passport.bilibili.com/login";
    public const string NavUrl = "https://api.bilibili.com/x/web-interface/nav";
    private readonly Task<CoreWebView2Environment> environment;
    private readonly List<Form> popups = [];
    private readonly List<WeakReference<WebView2>> views = [];
    public int VolumePercent { get; private set; } = 15;

    public BrowserSession(string folder, int volume = 15)
    {
        VolumePercent = Math.Clamp(volume, 0, 100);
        Directory.CreateDirectory(folder);
        environment = CoreWebView2Environment.CreateAsync(null, folder);
    }

    public async Task InitializeAsync(WebView2 view)
    {
        await view.EnsureCoreWebView2Async(await environment);
        view.CoreWebView2.Settings.IsStatusBarEnabled = false;
        view.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
        views.Add(new WeakReference<WebView2>(view));
        await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(PlayerScripts.Audio(VolumePercent));
        view.CoreWebView2.WebMessageReceived += async (_, e) =>
        {
            if (!Uri.TryCreate(e.Source, UriKind.Absolute, out var source) || source.Host != "live.bilibili.com") return;
            if (e.WebMessageAsJson.Contains("dd-audio-ready")) await ApplyVolumeAsync(view);
        };
        view.CoreWebView2.NewWindowRequested += async (_, e) =>
        {
            e.Handled = true;
            if (!e.IsUserInitiated) return;
            using var deferral = e.GetDeferral();
            var popup = new Form { Text = "哔哩哔哩", Size = new Size(1150, 800), StartPosition = FormStartPosition.CenterParent };
            var browser = new WebView2 { Dock = DockStyle.Fill };
            popup.Controls.Add(browser);
            popups.Add(popup);
            popup.FormClosed += (_, _) => { popups.Remove(popup); browser.Dispose(); };
            popup.Show(view.FindForm());
            try
            {
                await InitializeAsync(browser);
                e.NewWindow = browser.CoreWebView2;
            }
            catch (Exception error)
            {
                AppLog.Error("Browser popup failed", error);
                popup.Close();
            }
        };
    }

    public void ClosePopups()
    {
        foreach (Form popup in popups.ToArray()) popup.Close();
    }

    public async Task SetVolumeAsync(int percent)
    {
        VolumePercent = Math.Clamp(percent, 0, 100);
        foreach (var reference in views.ToArray())
        {
            if (!reference.TryGetTarget(out var view) || view.IsDisposed)
            {
                views.Remove(reference);
                continue;
            }
            await ApplyVolumeAsync(view);
        }
    }

    public async Task ApplyVolumeAsync(WebView2 view)
    {
        if (view.IsDisposed) return;
        string value = (VolumePercent / 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture);
        try { await view.ExecuteScriptAsync("window.__ddAudio?.setVolume(" + value + ")"); }
        catch (Exception error) { AppLog.Error("Volume update failed", error); }
    }

    public static async Task<bool> NavigateAsync(WebView2 view, string url, int timeoutMs = 15000)
    {
        var completion = new TaskCompletionSource<bool>();
        void Finished(object? sender, CoreWebView2NavigationCompletedEventArgs args) => completion.TrySetResult(args.IsSuccess);
        view.CoreWebView2.NavigationCompleted += Finished;
        try
        {
            view.CoreWebView2.Navigate(url);
            if (await Task.WhenAny(completion.Task, Task.Delay(timeoutMs)) != completion.Task)
            {
                view.CoreWebView2.Stop();
                return false;
            }
            return await completion.Task;
        }
        finally
        {
            if (!view.IsDisposed && view.CoreWebView2 != null)
                view.CoreWebView2.NavigationCompleted -= Finished;
        }
    }
}
