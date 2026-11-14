using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace ClipHarbor;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shot = Program.ScreenshotPath;
            // In screenshot mode use a fresh, empty state so the picture is predictable and nothing is changed.
            var state = shot != null ? new AppState() : AppState.Load();
            if (shot != null)
                foreach (var o in new[] { state.Settings.SmartOptions, state.Settings.LastOptions }) o.Folder = AppState.DefaultFolder();
            ApplyTheme(state.Settings.Theme);
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = new MainWindow(state, shot, Program.Links);
        }
        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(string name)
    {
        if (Current == null) return;
        Current.RequestedThemeVariant = name switch
        {
            "Dark" => ThemeVariant.Dark,
            "System" => ThemeVariant.Default,
            _ => ThemeVariant.Light,
        };
    }
}
