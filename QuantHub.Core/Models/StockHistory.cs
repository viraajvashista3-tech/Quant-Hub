namespace QuantHub.Core.Models;

public sealed class PriceBar
{
    public required string Date { get; init; }
    public double Open { get; init; }
    public double High { get; init; }
    public double Low { get; init; }
    public double Close { get; init; }
    public long Volume { get; init; }
    public double? Ma50 { get; init; }
    public double? Ma200 { get; init; }
    public double? Macd { get; init; }
    public double? MacdSignal { get; init; }
    public double? Rsi { get; init; }
    public double? BbUpper { get; init; }
    public double? BbLower { get; init; }
    public double? BbMa20 { get; init; }
}

public sealed class StockHistory
{
    public required string Ticker { get; init; }
    public required IReadOnlyList<PriceBar> Bars { get; init; }
}
