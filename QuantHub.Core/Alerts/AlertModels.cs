namespace QuantHub.Core.Alerts;

public enum AlertDirection
{
    Above,
    Below
}

public sealed record PriceAlert(
    Guid Id, string Ticker, AlertDirection Direction, double TargetPrice,
    DateTime CreatedAtUtc, DateTime? TriggeredAtUtc, double? TriggeredAtPrice);
