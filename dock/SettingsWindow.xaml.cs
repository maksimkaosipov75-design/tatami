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

        // The checked radio button in the indicator picker takes focus once the
        // window loads and asks the ScrollViewer to bring it into view, which
        // opens the window scrolled halfway down. Undo that after layout.
        Loaded += (_, _) => Dispatcher.BeginInvoke(
            new Action(Scroller.ScrollToTop),
            System.Windows.Threading.DispatcherPriority.Loaded);
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
