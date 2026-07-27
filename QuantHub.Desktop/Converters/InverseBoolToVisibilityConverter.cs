using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuantHub.Desktop.Converters;

/// <summary>Visible when the bound bool is false, Collapsed when true - the inverse of
/// BooleanToVisibilityConverter, used for "show this only when that other panel is hidden" pairs.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
