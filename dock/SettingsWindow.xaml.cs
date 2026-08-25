using System.Windows;
using OmarchyDock.Services;

namespace OmarchyDock;

public partial class SettingsWindow : Window
{
    private readonly DockSettings _settings;

    public SettingsWindow(DockSettings settings)
    {
        InitializeComponent();

        // Bound directly to the live settings object, so every slider drag is
        // applied by the dock immediately - the point of a settings window is
        // seeing the result while you adjust it, not after saving.
        _settings = settings;
        DataContext = settings;
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "Reset every dock setting to its default?",
            "Dock settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        _settings.CopyFrom(new DockSettings());
        _settings.Save();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _settings.Save();
    }
}
