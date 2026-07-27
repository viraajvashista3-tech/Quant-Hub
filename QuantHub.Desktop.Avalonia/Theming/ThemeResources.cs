using Avalonia.Controls;
using Avalonia.Media;

namespace QuantHub.Desktop.Theming;

/// <summary>Resolves a brush resource from code (converters, ViewModel computed properties, chart
/// building) with the current theme variant explicitly passed. Avalonia's non-ThemeVariant
/// TryFindResource/FindResource overloads do NOT reliably resolve resources declared inside
/// ResourceDictionary.ThemeDictionaries (Colors.axaml's Dark/Light dictionaries) - only the
/// ThemeVariant-parameterized overload does, and XAML's DynamicResource/StaticResource markup
/// extensions handle this internally, which is why the gap only shows up in code-behind lookups.</summary>
public static class ThemeResources
{
    /// <summary>Resolves a brush by theme-dictionary resource key (e.g. "PositiveBrush"). If not
    /// found as a resource, falls back to parsing the key itself as a literal color name/hex (e.g.
    /// "White") - lets callers pass either a resource key or a literal color through one API.</summary>
    public static IBrush GetBrush(string key)
    {
        var app = Avalonia.Application.Current;
        if (app is not null && app.TryFindResource(key, app.ActualThemeVariant, out var res) && res is IBrush brush)
        {
            return brush;
        }
        try
        {
            return Brush.Parse(key);
        }
        catch (FormatException)
        {
            return Brushes.Gray;
        }
    }

    public static Color GetColor(string key, Color? fallback = null) =>
        GetBrush(key) is ISolidColorBrush solid ? solid.Color : fallback ?? Colors.Gray;
}
