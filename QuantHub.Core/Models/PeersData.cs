namespace QuantHub.Core.Models;

public sealed class PeerStock
{
    public required string Ticker { get; init; }
    public string? Name { get; init; }
    public double? Price { get; init; }
    public double? Pe { get; init; }
    public double? ForwardPe { get; init; }
    public double? DividendYield { get; init; }
    public double? Beta { get; init; }
    public double? MarketCap { get; init; }
    public double? ProfitMargins { get; init; }
    public double? DebtToEquity { get; init; }
    public double? ReturnOnEquity { get; init; }
}

public sealed class PeersData
{
    public required string Ticker { get; init; }
    public required string Sector { get; init; }
    public string? Summary { get; init; }
    public required IReadOnlyList<PeerStock> Peers { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>? CorrelationMatrix { get; init; }
}
