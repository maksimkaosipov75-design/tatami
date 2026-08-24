using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace OmarchyDock.Converters;

internal class ForegroundToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isForeground = value is true;
        if (!isForeground) return Brushes.Transparent;

        return Application.Current.Resources["Surface0Brush"] as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
