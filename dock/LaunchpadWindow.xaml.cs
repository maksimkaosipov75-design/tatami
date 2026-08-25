using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using OmarchyDock.Models;
using OmarchyDock.Services;

namespace OmarchyDock;

public partial class LaunchpadWindow : Window
{
    private static readonly Duration OpenDuration = new(TimeSpan.FromMilliseconds(180));
    private static readonly Duration CloseDuration = new(TimeSpan.FromMilliseconds(130));

    private bool _isClosingAnimated;

    public LaunchpadWindow()
    {
        InitializeComponent();
        DataContext = StartMenuScanner.ScanApps();
        Loaded += LaunchpadWindow_Loaded;
    }

    private void LaunchpadWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Focus(); // so Esc reaches Window_KeyDown without needing a click first
        PlayOpenAnimation();
    }

    private void PlayOpenAnimation()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, OpenDuration) { EasingFunction = ease });
        GridScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.92, 1, OpenDuration) { EasingFunction = ease });
        GridScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.92, 1, OpenDuration) { EasingFunction = ease });
    }

    // Closing is animated, so Close() has to wait for the storyboard to finish;
    // _isClosingAnimated marks the second, real Close() so it isn't intercepted
    // again and we don't restart the animation on every stray click/Esc.
    private void AnimateThenClose()
    {
        if (_isClosingAnimated) return;
        _isClosingAnimated = true;

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        GridScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, 0.94, CloseDuration) { EasingFunction = ease });
        GridScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1, 0.94, CloseDuration) { EasingFunction = ease });

        var fade = new DoubleAnimation(Opacity, 0, CloseDuration) { EasingFunction = ease };
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) AnimateThenClose();
    }

    private void Backdrop_Click(object sender, MouseButtonEventArgs e)
    {
        AnimateThenClose();
    }

    private void PinToDock_Click(object sender, RoutedEventArgs e)
    {
        // The context menu's DataContext is the tile's AppEntry.
        if (sender is not FrameworkElement { DataContext: AppEntry app }) return;
        if (Application.Current.MainWindow is not MainWindow dock) return;

        dock.PinPath(app.TargetPath, app.Name);
        AnimateThenClose();
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
        AnimateThenClose();
    }
}
