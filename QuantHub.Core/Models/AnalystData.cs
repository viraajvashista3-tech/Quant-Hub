namespace QuantHub.Core.Models;

public sealed class AnalystAction
{
    public required string Firm { get; init; }
    public string? ToGrade { get; init; }
    public string? FromGrade { get; init; }
    public string? Date { get; init; }
    public required string Action { get; init; }
    public string? PriceTargetAction { get; init; }
    public double? CurrentPriceTarget { get; init; }
    public double? PriorPriceTarget { get; init; }
}

public sealed class RecommendationTrendPoint
{
    public required string Period { get; init; }
    public int? StrongBuy { get; init; }
    public int? Buy { get; init; }
    public int? Hold { get; init; }
    public int? Sell { get; init; }
    public int? StrongSell { get; init; }
}

public sealed class AnalystData
{
    public required string Ticker { get; init; }
    public required string ConsensusRating { get; init; }
    public int? NumAnalysts { get; init; }
    public double? CurrentPrice { get; init; }
    public double? TargetLow { get; init; }
    public double? TargetMean { get; init; }
    public double? TargetHigh { get; init; }
    public IReadOnlyList<AnalystAction>? RecentActions { get; init; }
    public IReadOnlyList<RecommendationTrendPoint>? RecommendationTrend { get; init; }
}
