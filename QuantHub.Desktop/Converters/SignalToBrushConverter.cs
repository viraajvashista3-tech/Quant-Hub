using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using QuantHub.Core.Models;

namespace QuantHub.Desktop.Converters;

public sealed class SignalToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value switch
        {
            Signal.Buy => "PositiveBrush",
            Signal.Avoid => "DestructiveBrush",
            _ => "WarningBrush"
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
