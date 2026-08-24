using System.Windows;
using OmarchyDock.Services;

namespace OmarchyDock;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Load before base.OnStartup() creates the StartupUri window, so its
        // DynamicResource lookups resolve on first paint instead of racing it.
        ThemeLoader.LoadIntoApplicationResources();
        base.OnStartup(e);
    }
}
