namespace QuantHub.Core.Models;

public enum Signal
{
    Buy,
    Hold,
    Avoid
}

public sealed class StockOverview
{
    public required string Ticker { get; init; }
    public required string Name { get; init; }
    public double Price { get; init; }
    public double Change { get; init; }
    public double ChangePercent { get; init; }
    public double QuantScore { get; init; }
    public Signal Signal { get; init; }
    public double SentimentScore { get; init; }
    public long Volume { get; init; }
    public long AvgVolume { get; init; }
    public double Rsi { get; init; }
    public double? Ma50 { get; init; }
    public double? Ma200 { get; init; }
    public double Macd { get; init; }
    public double MacdSignal { get; init; }
    public string? Sector { get; init; }
    public double? Beta { get; init; }
    public double? AnnualizedVolatility { get; init; }
    public double? SharpeRatio { get; init; }
    public double? MaxDrawdown { get; init; }
    public double? TrendScore { get; init; }
    public double? MomentumScore { get; init; }
    public double? MacdScore { get; init; }
    public double? SentimentContrib { get; init; }
    public double? SentimentWeightMultiplier { get; init; }
    public double? MeanReversionScore { get; init; }
    public double? PriceMomentumScore { get; init; }
    public double? BollingerPctB { get; init; }
    public double? PriceRoc21Pct { get; init; }
    public double? VolScore { get; init; }
    public double? VolRatio { get; init; }
    public bool? AboveMa50 { get; init; }
    public bool? AboveMa200 { get; init; }
    public bool? GoldenCross { get; init; }
}
