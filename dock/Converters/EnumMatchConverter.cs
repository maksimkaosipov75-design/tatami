using System.Globalization;
using System.Windows.Data;

namespace OmarchyDock.Converters;

/// <summary>
/// Two-way binding between an enum property and a set of radio buttons: each
/// button is checked when the property equals its ConverterParameter, and
/// checking one writes that value back.
/// </summary>
internal class EnumMatchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && value.Equals(parameter);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        // Unchecking is ignored: the group's other button reports the new value.
        value is true && parameter is not null ? parameter : Binding.DoNothing;
}
