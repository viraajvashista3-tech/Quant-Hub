namespace QuantHub.Core.Analysis;

/// <summary>
/// Hand-picked (not backtested - there's no historical news archive to validate sentiment against,
/// see BacktestEngine) per-sector multipliers on QuantScoreCalculator's sentiment contribution,
/// reflecting how much headline-driven narrative typically moves a sector's stocks versus
/// fundamentals/macro. Technology and Communication Services (megacap tech, AI, media/social
/// platforms) trade heavily on sentiment and hype; regulated/defensive sectors (Utilities, Consumer
/// Defensive) barely react to headlines at all. Sector names match UniverseData.Sectors / Yahoo's
/// assetProfile.sector strings exactly (same convention StockAnalysisService.ResolveSectorAndPeersAsync
/// already relies on) - an unrecognized or missing sector falls back to DefaultMultiplier (1.0),
/// i.e. today's un-adjusted behavior.
/// </summary>
public static class SectorSentimentWeights
{
    public const double DefaultMultiplier = 1.0;

    private static readonly Dictionary<string, double> Multipliers = new()
    {
        ["Technology"] = 1.5,
        ["Communication Services"] = 1.3,
        ["Consumer Cyclical"] = 1.2,
        ["Healthcare"] = 1.1,
        ["Financial Services"] = 1.0,
        ["Industrials"] = 0.9,
        ["Energy"] = 0.9,
        ["Basic Materials"] = 0.9,
        ["Real Estate"] = 0.7,
        ["Consumer Defensive"] = 0.7,
        ["Utilities"] = 0.6
    };

    public static double ForSector(string? sector) =>
        sector is not null && Multipliers.TryGetValue(sector, out var m) ? m : DefaultMultiplier;
}
