using System.IO;
using System.Windows;
using System.Windows.Threading;
using OmarchyDock.Services;

namespace OmarchyDock;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "dotfiles", "dock", "omarchydock.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Load before base.OnStartup() creates the StartupUri window, so its
        // DynamicResource lookups resolve on first paint instead of racing it.
        ThemeLoader.LoadIntoApplicationResources();

        // This app is meant to sit running all session as a shell component, so
        // a fault in one feature (a launcher click, an icon that won't load)
        // should be logged and swallowed rather than taking the dock down with it.
        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception);
            args.Handled = true;
        };

        base.OnStartup(e);
    }

    private static void Log(Exception ex)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never itself crash the app.
        }
    }
}
