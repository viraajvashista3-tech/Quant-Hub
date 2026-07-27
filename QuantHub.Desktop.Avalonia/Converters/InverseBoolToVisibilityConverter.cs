using System.Globalization;
using Avalonia.Data.Converters;

namespace QuantHub.Desktop.Converters;

/// <summary>True (visible) when the bound bool is false, false (hidden) when true - the inverse of
/// a plain bool binding, used for "show this only when that other panel is hidden" pairs. Bound to
/// IsVisible rather than a Visibility enum (Avalonia has no WPF-style Visibility type).</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
