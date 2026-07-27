namespace QuantHub.Core.Models;

public sealed class MarketPulseItem
{
    public required string Symbol { get; init; }
    public required string Label { get; init; }
    public double Price { get; init; }
    public double Change { get; init; }
    public double ChangePct { get; init; }
    public double Change1wPct { get; init; }
    public double Change1mPct { get; init; }
}

public sealed class MarketPulseData
{
    public required IReadOnlyList<MarketPulseItem> Indices { get; init; }
    public required IReadOnlyList<MarketPulseItem> Sectors { get; init; }
    public required IReadOnlyList<MarketPulseItem> Macro { get; init; }
    public double Vix { get; init; }
    public required string MarketMood { get; init; }
    public required string RotationNote { get; init; }
}

public sealed class UniverseSector
{
    public required string Sector { get; init; }
    public required IReadOnlyList<string> Tickers { get; init; }
}
