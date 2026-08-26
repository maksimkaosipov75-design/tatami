using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Pier.Converters;

internal class ForegroundToBrushConverter : IValueConverter
{
    // The dock is a layered window, and a layered window does not receive mouse
    // input on pixels whose alpha is zero. A fully transparent icon background
    // therefore leaves the icon clickable only where its artwork happens to be
    // opaque - which for the Launchpad's nine dots meant hovering a gap between
    // dots dropped the hover, shrank the icon, slid a dot under the cursor and
    // started over. One unit of alpha is invisible and hit-testable.
    private static readonly Brush Invisible = CreateInvisible();

    private static Brush CreateInvisible()
    {
        var brush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isForeground = value is true;
        if (!isForeground) return Invisible;

        // Written by MainWindow.ApplySettings, so it tracks the dock's own
        // background opacity. Falls back to flat Surface0 for the first render,
        // before any settings have been applied.
        return Application.Current.Resources["DockIconActiveBrush"] as Brush
               ?? Application.Current.Resources["Surface0Brush"] as Brush
               ?? Invisible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
