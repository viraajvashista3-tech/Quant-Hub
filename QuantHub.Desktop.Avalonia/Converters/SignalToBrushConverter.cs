using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using QuantHub.Core.Models;

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
        return Avalonia.Application.Current?.TryFindResource(key, out var res) == true ? res as IBrush : Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
