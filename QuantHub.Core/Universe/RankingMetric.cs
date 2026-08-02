namespace QuantHub.Core.Universe;

/// <summary>The three ways a set of tickers can be ranked "best to buy first" - shared by the
/// Watchlist ranking table and the Universe Top 20 ranking, so both mean the same thing by each
/// metric name.</summary>
public enum RankingMetric
{
    QuantScore,
    UpsidePotential,
    AnalystRating
}
