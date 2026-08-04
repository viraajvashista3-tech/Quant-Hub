namespace QuantHub.Core.Alerts;

/// <summary>Pure alert-triggering logic, kept dependency-free like every other Core analysis
/// module - AlertService (Desktop) owns fetching prices and persistence; this just answers "given
/// this price, has this alert fired?" and "what should the notification say?"</summary>
public static class AlertEvaluator
{
    public static bool IsTriggered(PriceAlert alert, double currentPrice) => alert.Direction switch
    {
        AlertDirection.Above => currentPrice >= alert.TargetPrice,
        AlertDirection.Below => currentPrice <= alert.TargetPrice,
        _ => false
    };

    /// <summary>Reads TriggeredAtPrice (not a separate currentPrice parameter) so this can be called
    /// straight off a persisted/already-triggered PriceAlert without the caller needing to keep the
    /// triggering price around separately.</summary>
    public static string FormatTriggerMessage(PriceAlert alert)
    {
        var direction = alert.Direction == AlertDirection.Above ? "above" : "below";
        var price = alert.TriggeredAtPrice?.ToString("0.00") ?? alert.TargetPrice.ToString("0.00");
        return $"{alert.Ticker} is now {direction} ${alert.TargetPrice:0.00} (currently ${price}).";
    }
}
