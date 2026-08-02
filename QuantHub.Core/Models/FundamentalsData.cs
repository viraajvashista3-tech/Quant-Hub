namespace QuantHub.Core.Models;

public sealed class FundamentalsData
{
    public required string Ticker { get; init; }
    public required string Name { get; init; }
    public double? MarketCap { get; init; }
    public double? Pe { get; init; }
    public double? ForwardPe { get; init; }
    public double? Peg { get; init; }
    public double? PriceToBook { get; init; }
    public double? EvToEbitda { get; init; }
    public double? DebtToEquity { get; init; }
    public double? ReturnOnEquity { get; init; }
    public double? ReturnOnAssets { get; init; }
    public double? OperatingMargins { get; init; }
    public double? ProfitMargins { get; init; }
    public double? Beta { get; init; }
    public double? DividendYield { get; init; }
    public double? DividendRate { get; init; }
    public double? PayoutRatio { get; init; }
    public double? Eps { get; init; }
    public double? BookValuePerShare { get; init; }
    public double? GrahamNumber { get; init; }
    public string? Sector { get; init; }
    public string? Industry { get; init; }
    public string? Description { get; init; }
    public double? FiftyTwoWeekHigh { get; init; }
    public double? FiftyTwoWeekLow { get; init; }
    public double? ShortRatio { get; init; }
    public double? InstitutionalOwnership { get; init; }
    public double? ShortPercentOfFloat { get; init; }
    public double? RevenueGrowth { get; init; }
    public double? EarningsGrowth { get; init; }
    public double? CurrentRatio { get; init; }
    public double? QuickRatio { get; init; }
    public double? TotalRevenue { get; init; }
    public double? FreeCashflow { get; init; }
    public double? TotalDebt { get; init; }
    public double? TotalCash { get; init; }
    public double? SharesOutstanding { get; init; }
}
