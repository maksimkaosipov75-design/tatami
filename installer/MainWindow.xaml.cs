using System.Security.Principal;
using System.Text;
using System.Windows;
using TatamiSetup.Models;
using TatamiSetup.Services;

namespace TatamiSetup;

public partial class MainWindow : Window
{
    private readonly List<Component> _components = Component.BuildCatalog();
    private readonly StringBuilder _log = new();

    public MainWindow()
    {
        InitializeComponent();
        ComponentList.ItemsSource = _components;

        if (!IsElevated())
        {
            StatusText.Text = "Not running as administrator — the font and some tweaks may be skipped.";
        }
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        var anyUnselected = _components.Any(c => !c.IsSelected);
        foreach (var component in _components) component.IsSelected = anyUnselected;
        SelectAllButton.Content = anyUnselected ? "Select none" : "Select all";
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        SelectAllButton.IsEnabled = false;
        StatusText.Text = "Installing…";

        var runner = new InstallRunner(Append);

        try
        {
            await runner.RunAsync(_components);
            StatusText.Text = "Finished. Sign out and back in to start everything automatically.";
        }
        catch (Exception ex)
        {
            Append($"FAILED: {ex}");
            StatusText.Text = "Finished with errors — see the log.";
        }
        finally
        {
            InstallButton.IsEnabled = true;
            SelectAllButton.IsEnabled = true;
        }
    }

    private void Append(string line)
    {
        // Called from the install worker thread.
        Dispatcher.Invoke(() =>
        {
            _log.AppendLine(line);
            LogBox.Text = _log.ToString();
            LogScroller.ScrollToEnd();
        });
    }
}
