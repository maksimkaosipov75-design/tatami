using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using OmarchyDock.Models;
using OmarchyDock.Services;

namespace OmarchyDock;

public partial class LaunchpadWindow : Window
{
    public LaunchpadWindow()
    {
        InitializeComponent();
        DataContext = StartMenuScanner.ScanApps();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void Backdrop_Click(object sender, MouseButtonEventArgs e)
    {
        Close();
    }

    private void AppTile_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // stop bubbling to Backdrop_Click - we close ourselves below
        if (sender is not FrameworkElement { Tag: AppEntry app }) return;

        try
        {
            Process.Start(new ProcessStartInfo(app.TargetPath) { UseShellExecute = true });
        }
        catch
        {
            // Broken/missing target - nothing sensible to do from here.
        }
        Close();
    }
}
