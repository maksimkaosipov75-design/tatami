using System.Windows;
using System.Windows.Threading;
using OmarchyDock.Services;

namespace OmarchyDock;

public partial class App : Application
{
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
            Diagnostics.Log(args.Exception);
            args.Handled = true;
        };

        Diagnostics.Log(
            $"render tier={System.Windows.Media.RenderCapability.Tier >> 16} " +
            $"maxHwTexture={System.Windows.Media.RenderCapability.MaxHardwareTextureSize}");

        base.OnStartup(e);
    }
}
