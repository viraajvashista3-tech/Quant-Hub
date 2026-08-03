namespace QuantHub.Core.Portfolio;

/// <summary>A user-entered position: what they actually paid, not a Quant Score prediction. Kept
/// separate from PredictionLog's LoggedPrediction - that grades the algorithm's own calls, this
/// tracks the user's real decisions, a genuinely different question ("how am I doing" rather than
/// "is the Quant Score right").</summary>
public sealed record Position(string Ticker, double Shares, double EntryPrice, DateOnly EntryDate, double EntryBenchmarkPrice);

public sealed record PositionPerformance(
    string Ticker, double Shares, double EntryPrice, DateOnly EntryDate,
    double CurrentPrice, double CostBasis, double MarketValue,
    double GainLossDollar, double GainLossPct, double ExcessReturnVsBenchmarkPct);

public sealed record PortfolioSummary(
    double TotalCostBasis, double TotalMarketValue, double TotalGainLossDollar,
    double TotalGainLossPct, double WeightedExcessReturnVsBenchmarkPct);
