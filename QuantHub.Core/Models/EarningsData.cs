namespace QuantHub.Core.Models;

public sealed class EarningsQuarter
{
    public required string Date { get; init; }
    public double? EpsActual { get; init; }
    public double? EpsEstimate { get; init; }
    public double? SurprisePercent { get; init; }
}

public sealed class EarningsData
{
    public required string Ticker { get; init; }
    public required IReadOnlyList<EarningsQuarter> History { get; init; }
    public string? NextEarningsDate { get; init; }
    public string? ExDividendDate { get; init; }
}
