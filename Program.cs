using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ClipHarbor;

static class Program
{
    public static string? ScreenshotPath;
    public static readonly List<string> Links = new();
    /// <summary>Started at sign-in: stay hidden in the tray.</summary>
    public static bool StartHidden;

    [STAThread]
    static int Main(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--screenshot" && i + 1 < args.Length) ScreenshotPath = args[++i];
            else if (args[i] == StartAtLogin.Flag) StartHidden = true;
            else if (args[i] == Updater.AfterUpdateFlag && i + 1 < args.Length && int.TryParse(args[++i], out var oldPid))
                Updater.FinishUpdate(oldPid); // the old version is closing; wait for it, then tidy up
            else if (Engine.IsUrl(args[i]) || BrowserLink.IsLink(args[i])) Links.Add(args[i]);
        }

        // Already running? Pass the links on (or ask it to show its window) and stop here.
        if (ScreenshotPath == null)
        {
            var message = StartHidden && Links.Count == 0 ? new List<string>() : new List<string>(Links) { SingleInstance.Show };
            if (SingleInstance.TryHandOff(message)) return 0;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" })
            .LogToTrace();
}
