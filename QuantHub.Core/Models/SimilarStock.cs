namespace QuantHub.Core.Models;

/// <summary>Lightweight snapshot for the Universe page's "similar stocks" cards - same-sector peers
/// of the active ticker, with just enough data (price/change) to render a clickable card.</summary>
public sealed class SimilarStock
{
    public required string Ticker { get; init; }
    public string? Name { get; init; }
    public required string Sector { get; init; }
    public double? Price { get; init; }
    public double? ChangePercent { get; init; }
}
