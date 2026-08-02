using QuantHub.Core.Backtesting;
using QuantHub.Core.Models;

namespace QuantHub.Core.Universe;

/// <summary>One ticker's data as of a universe sweep - carries the same QuantScore component
/// breakdown PredictionLog already tracks per ticker (so ExplainTopNChanges can reuse
/// PredictionLog.TopComponentDrivers's exact "what moved the most" rule) plus the analyst-target
/// fields AnalystAnalyzer computes, so a single sweep can rank by any of the three RankingMetric
/// values without a second fetch.</summary>
public sealed record TickerRankData(
    string Ticker, string Name, double Price, double QuantScore, Signal Signal,
    double? TrendScore, double? MomentumScore, double? MacdScore, double? VolScore,
    double? MeanReversionScore, double? PriceMomentumScore, double? SentimentContrib,
    double? TargetMean, double? UpsidePotentialPct, string? ConsensusRating, int AnalystRatingRank);

/// <summary>One full-universe sweep's result - RanAtUtc is both the "how fresh is this" timestamp and
/// the clock UpdateMonthlyArchive keys off (never DateTime.UtcNow read internally), so archiving
/// behavior is fully driven by the caller and unit-testable with synthetic dates.</summary>
public sealed record UniverseSnapshot(
    DateTime RanAtUtc, IReadOnlyList<TickerRankData> Tickers, IReadOnlyList<string> SkippedTickers);

/// <summary>One ticker's Top-N status change between two snapshots, with a deterministic,
/// template-built explanation of what happened and (metric-specifically) why.</summary>
public sealed record TickerRankChange(string Ticker, int? PreviousRank, int? CurrentRank, string Explanation);

/// <summary>Pure ranking/archiving/explanation logic behind the Universe page's Top 20 - no network,
/// no DI, no ambient clock (every date-sensitive method takes its "now" as a parameter). The
/// network/persistence/scheduling layer around this lives in
/// QuantHub.Desktop.Services.UniverseRankingService, the same Core-pure-logic/Desktop-orchestration
/// split BacktestEngine (Core) / AutoBacktestService (Desktop) already uses.</summary>
public static class UniverseRanking
{
    /// <summary>Orders a set of tickers "best to buy first" by the given metric. UpsidePotential and
    /// AnalystRating both exclude tickers with no usable data for that metric (no price target; no
    /// analyst coverage, including Yahoo's own "N/A") rather than sorting them to the bottom with a
    /// sentinel value - they simply aren't rankable by that metric this sweep.</summary>
    public static IReadOnlyList<TickerRankData> RankByMetric(IReadOnlyList<TickerRankData> tickers, RankingMetric metric) => metric switch
    {
        RankingMetric.UpsidePotential => tickers
            .Where(t => t.UpsidePotentialPct is not null)
            .OrderByDescending(t => t.UpsidePotentialPct)
            .ToList(),
        RankingMetric.AnalystRating => tickers
            .Where(t => t.AnalystRatingRank < 5)
            .OrderBy(t => t.AnalystRatingRank)
            .ThenByDescending(t => t.QuantScore)
            .ToList(),
        _ => tickers.OrderByDescending(t => t.QuantScore).ToList()
    };

    public static IReadOnlyList<TickerRankData> TopN(IReadOnlyList<TickerRankData> tickers, RankingMetric metric, int n = 20) =>
        RankByMetric(tickers, metric).Take(n).ToList();

    /// <summary>Archives newSnapshot under its own calendar month (yyyy-MM, by RanAtUtc) the first
    /// time that month is seen - a later sweep within the same month leaves the archive untouched, so
    /// the monthly snapshot always reflects "this ranking as of the start of the month" rather than
    /// whichever sweep happened to run last. Keeps only the 2 most recent distinct months. Pure and
    /// synchronous: takes the new snapshot's own timestamp rather than reading an ambient clock, so
    /// month-boundary behavior is fully exercisable with synthetic dates.</summary>
    public static IReadOnlyDictionary<string, UniverseSnapshot> UpdateMonthlyArchive(
        IReadOnlyDictionary<string, UniverseSnapshot> existingArchive, UniverseSnapshot newSnapshot)
    {
        var monthKey = newSnapshot.RanAtUtc.ToString("yyyy-MM");
        if (existingArchive.ContainsKey(monthKey)) return existingArchive;

        var updated = new Dictionary<string, UniverseSnapshot>(existingArchive) { [monthKey] = newSnapshot };
        return updated
            .OrderByDescending(kv => kv.Key, StringComparer.Ordinal)
            .Take(2)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>The archived snapshot that isn't the current calendar month, or null if fewer than 2
    /// distinct months have been archived yet - "not enough history to compare against" isn't an
    /// error, just means ExplainTopNChanges has nothing to explain yet.</summary>
    public static UniverseSnapshot? PreviousMonthSnapshot(IReadOnlyDictionary<string, UniverseSnapshot> archive, DateTime nowUtc)
    {
        var currentMonthKey = nowUtc.ToString("yyyy-MM");
        return archive
            .Where(kv => kv.Key != currentMonthKey)
            .OrderByDescending(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Value)
            .FirstOrDefault();
    }

    /// <summary>Compares this month's Top N (by the given metric) against last month's and explains
    /// every meaningful change: entered/left the Top N, or moved at least minRankMoveToExplain spots
    /// within it. Null previous -> [] (not enough history yet), mirroring
    /// PredictionLog.ExplainScoreChange's same convention. Each explanation's second sentence is
    /// metric-specific (QuantScore reuses PredictionLog.TopComponentDrivers against the two
    /// snapshots' component breakdown; UpsidePotential reports the target-price move; AnalystRating
    /// reports the consensus-rating change, or that the ticker simply moved relative to same-rated
    /// peers if the rating itself didn't change).</summary>
    public static IReadOnlyList<TickerRankChange> ExplainTopNChanges(
        UniverseSnapshot? previous, UniverseSnapshot current, RankingMetric metric, int n = 20, int minRankMoveToExplain = 3)
    {
        if (previous is null) return [];

        var currentRankByTicker = ToRankDictionary(RankByMetric(current.Tickers, metric));
        var previousRankByTicker = ToRankDictionary(RankByMetric(previous.Tickers, metric));
        var previousByTicker = previous.Tickers.ToDictionary(t => t.Ticker);
        var currentByTicker = current.Tickers.ToDictionary(t => t.Ticker);

        var tickersOfInterest = currentRankByTicker.Where(kv => kv.Value <= n).Select(kv => kv.Key)
            .Union(previousRankByTicker.Where(kv => kv.Value <= n).Select(kv => kv.Key));

        var changes = new List<TickerRankChange>();
        foreach (var ticker in tickersOfInterest)
        {
            int? currRank = currentRankByTicker.TryGetValue(ticker, out var cr) ? cr : null;
            int? prevRank = previousRankByTicker.TryGetValue(ticker, out var pr) ? pr : null;
            var isInCurrentTopN = currRank is { } c && c <= n;
            var isInPreviousTopN = prevRank is { } p && p <= n;

            string headline;
            if (isInCurrentTopN && prevRank is null)
            {
                headline = $"{ticker} entered the Top {n} at #{currRank} on newly available data.";
            }
            else if (isInCurrentTopN && !isInPreviousTopN)
            {
                headline = $"{ticker} entered the Top {n} at #{currRank}, up from #{prevRank} last month.";
            }
            else if (!isInCurrentTopN && isInPreviousTopN)
            {
                var currRankText = currRank is { } cr2 ? $"#{cr2}" : $"outside the Top {n}";
                headline = $"{ticker} dropped out of the Top {n}, falling from #{prevRank} to {currRankText}.";
            }
            else if (isInCurrentTopN && isInPreviousTopN && currRank is { } cc && prevRank is { } pp
                     && Math.Abs(pp - cc) >= minRankMoveToExplain)
            {
                var verb = cc < pp ? "climbed" : "fell";
                headline = $"{ticker} {verb} from #{pp} to #{cc} in the Top {n}.";
            }
            else
            {
                continue; // still ranked, moved less than the threshold - not worth calling out
            }

            var detail = BuildDetailSentence(metric, previousByTicker.GetValueOrDefault(ticker), currentByTicker.GetValueOrDefault(ticker));
            var explanation = detail is null ? headline : $"{headline} {detail}";
            changes.Add(new TickerRankChange(ticker, prevRank, currRank, explanation));
        }

        return changes.OrderBy(c => c.CurrentRank ?? int.MaxValue).ToList();
    }

    private static Dictionary<string, int> ToRankDictionary(IReadOnlyList<TickerRankData> ranked) =>
        ranked.Select((t, i) => (t.Ticker, Rank: i + 1)).ToDictionary(x => x.Ticker, x => x.Rank);

    private static string? BuildDetailSentence(RankingMetric metric, TickerRankData? prev, TickerRankData? curr)
    {
        if (curr is null) return null;
        return metric switch
        {
            RankingMetric.UpsidePotential => UpsideDetail(prev, curr),
            RankingMetric.AnalystRating => RatingDetail(prev, curr),
            _ => QuantScoreDetail(prev, curr)
        };
    }

    private static string QuantScoreDetail(TickerRankData? prev, TickerRankData curr)
    {
        if (prev is null) return $"Its Quant Score is currently {curr.QuantScore:0}.";

        (string Label, double? FirstVal, double? LastVal)[] components =
        [
            ("Trend", prev.TrendScore, curr.TrendScore),
            ("Momentum", prev.MomentumScore, curr.MomentumScore),
            ("MACD", prev.MacdScore, curr.MacdScore),
            ("Volume", prev.VolScore, curr.VolScore),
            ("Mean Reversion", prev.MeanReversionScore, curr.MeanReversionScore),
            ("Short-Term Reversal", prev.PriceMomentumScore, curr.PriceMomentumScore),
            ("News Sentiment", prev.SentimentContrib, curr.SentimentContrib)
        ];
        var drivers = PredictionLog.TopComponentDrivers(components);
        var scoreText = $"Its score moved from {prev.QuantScore:0} to {curr.QuantScore:0} points";
        return drivers.Count == 0
            ? $"{scoreText}."
            : $"{scoreText}, driven mainly by {string.Join(" and ", drivers.Select(d => d.Label))}.";
    }

    private static string? UpsideDetail(TickerRankData? prev, TickerRankData curr)
    {
        if (curr.TargetMean is not { } currTarget) return null;
        return prev?.TargetMean is not { } prevTarget
            ? $"Its price target is currently ${currTarget:0.00}, implying {curr.UpsidePotentialPct:0.0}% upside."
            : $"Its price target moved from ${prevTarget:0.00} to ${currTarget:0.00}, now implying {curr.UpsidePotentialPct:0.0}% upside.";
    }

    private static string? RatingDetail(TickerRankData? prev, TickerRankData curr)
    {
        if (curr.ConsensusRating is not { } currRating) return null;
        return prev?.ConsensusRating is { } prevRating && prevRating != currRating
            ? $"Its consensus rating changed from {prevRating} to {currRating}."
            : $"Its ranking shifted relative to other {currRating}-rated peers.";
    }
}
