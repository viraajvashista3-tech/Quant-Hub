using System.Globalization;
using Avalonia.Data.Converters;
using QuantHub.Core.Models;
using QuantHub.Desktop.Theming;

namespace QuantHub.Desktop.Converters;

public sealed class SignalToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            Signal.Buy => "PositiveBrush",
            Signal.Avoid => "DestructiveBrush",
            _ => "WarningBrush"
        };
        return ThemeResources.GetBrush(key);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
