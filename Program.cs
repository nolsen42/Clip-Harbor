using Avalonia;
using Avalonia.Media;

namespace ClipHarbor;

static class Program
{
    public static string? ScreenshotPath;
    public static readonly List<string> Links = new();

    [STAThread]
    static int Main(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--screenshot" && i + 1 < args.Length) ScreenshotPath = args[++i];
            else if (Engine.IsUrl(args[i])) Links.Add(args[i]);
        }
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" })
            .LogToTrace();
}
