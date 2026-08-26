using System.Windows;
using Pier.Services;

namespace Pier;

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

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // Tabbing into a radio group lands on its checked member, and on
        // activation WPF does that for us - so the window opened scrolled down
        // to whichever indicator or animation was selected. Undoing it has to
        // wait until here: at Loaded the focus move hasn't happened yet.
        Scroller.ScrollToTop();
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

    /// <summary>The window is borderless, so the header stands in for the title bar.</summary>
    private void Header_Drag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _settings.Save();
    }
}
