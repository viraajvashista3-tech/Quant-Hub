using System.Globalization;
using System.Windows.Data;

namespace QuantHub.Desktop.Converters;

/// <summary>Plain equality check for a MultiBinding DataTrigger (values[0] == values[1]). Deliberately
/// returns a bool rather than resolving a themed Brush - a converter that resolves DynamicResource
/// brushes internally (the old PeriodEqualsConverter) bakes a one-time snapshot into the target
/// property, which never refreshes if the theme changes afterward without the compared values
/// themselves changing. Pairing this bool with a Style's own DynamicResource Setters keeps every
/// selected/unselected pill (period tabs, theme toggle, difficulty cards) reactive to theme swaps.</summary>
public sealed class IsEqualConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values is [{ } a, { } b] && a.Equals(b);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
