using System.Globalization;
using Avalonia.Data.Converters;

namespace QuantHub.Desktop.Converters;

/// <summary>True (visible) when the bound value is a non-null, non-empty string, or any other
/// non-null reference/value - false (hidden) when null or an empty string. Used both for "is there
/// an error message to show" and "has this record loaded yet" checks, bound to IsVisible.</summary>
public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return false;
        if (value is string s && string.IsNullOrEmpty(s)) return false;
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
