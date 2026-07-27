using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuantHub.Desktop.Converters;

/// <summary>Visible when the bound value is a non-null, non-empty string, or any other non-null
/// reference/value - Collapsed when null or an empty string. Used both for "is there an error
/// message to show" and "has this record loaded yet" checks.</summary>
public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return Visibility.Collapsed;
        if (value is string s && string.IsNullOrEmpty(s)) return Visibility.Collapsed;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
