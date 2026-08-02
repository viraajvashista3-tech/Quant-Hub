using SkiaSharp;

namespace QuantHub.Desktop.Theming;

/// <summary>Single source of truth for chart/series colors LiveChartsCore needs as raw SKColor -
/// reads the live value of the corresponding Avalonia brush resource each call, so charts stay
/// theme/accent consistent instead of carrying their own hardcoded hex literals.</summary>
public static class ChartPalette
{
    public static SKColor Primary => Resolve("PrimaryBrush");
    public static SKColor Positive => Resolve("PositiveBrush");
    public static SKColor Destructive => Resolve("DestructiveBrush");
    public static SKColor Warning => Resolve("WarningBrush");
    public static SKColor AxisText => Resolve("MutedTextBrush");
    public static SKColor AxisLine => Resolve("PanelBorderBrush");
    public static SKColor ChartAccent2 => Resolve("ChartAccent2Brush");
    public static SKColor ChartAccent3 => Resolve("ChartAccent3Brush");
    public static SKColor Upgrade => Resolve("UpgradeBrush");
    public static SKColor Downgrade => Resolve("DowngradeBrush");
    public static SKColor UniverseAccent => Resolve("UniverseAccentBrush");
    public static SKColor FundamentalsAccent => Resolve("FundamentalsAccentBrush");
    public static SKColor InsiderAccent => Resolve("InsiderAccentBrush");
    public static SKColor MarketPulseAccent => Resolve("MarketPulseAccentBrush");

    private static SKColor Resolve(string key)
    {
        var c = ThemeResources.GetColor(key);
        return new SKColor(c.R, c.G, c.B, c.A);
    }
}
