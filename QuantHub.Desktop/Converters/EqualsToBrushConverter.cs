using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using QuantHub.Desktop.Theming;

namespace QuantHub.Desktop.Converters;

/// <summary>Multi-value equality-to-brush switch for "selected pill" UI (period tabs, theme/mode
/// toggles): values[0] is the item's own value, values[1] is the ambient currently-selected value:
/// resolves to the ConverterParameter's first ("true") brush key when equal, second ("false") key
/// otherwise. ConverterParameter format: "TrueBrushKeyOrColorName|FalseBrushKeyOrColorName". Exists
/// because Avalonia's Classes.name attribute binding only accepts a single Binding, not a
/// MultiBinding - this binds Background/Foreground directly instead, which do support MultiBinding
/// in object-element form.</summary>
public sealed class EqualsToBrushConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var equal = values is [{ } a, { } b] && a.Equals(b);
        var parts = (parameter as string)?.Split('|') ?? ["PrimaryBrush", "SurfaceBrush"];
        var key = equal ? parts[0] : parts[1];
        return ThemeResources.GetBrush(key);
    }
}
