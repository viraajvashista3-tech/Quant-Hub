using QuantHub.Core.Models;

namespace QuantHub.Core.Backtesting;

/// <summary>One live Quant Score call, logged the first time a ticker is viewed on a given UTC day.
/// Price/BenchmarkPrice are the ticker's and SPY's close at logging time; ExcessReturnPct/Hit/
/// EvaluatedAtUtc stay null until evaluated against reality, at least MaturityDays later - see
/// QuantHub.Desktop.Services.PredictionLogService, which owns persistence/network fetching for this
/// record. The record itself lives in Core (not Desktop, where the rest of that service lives) purely
/// so ComputeStats below is unit-testable the same way BacktestEngine's pure helpers are, without
/// pulling Desktop's Avalonia/network dependencies into the test project.
///
/// The seven optional component fields (added after the original nine) mirror exactly what the
/// Terminal page's "Quant Score Breakdown" card already shows live - only the components that
/// actually contribute to live scoring (RelativeStrength/InsiderPurchase/EarningsSurprise are
/// deliberately excluded; see backtest_feature memory - they're computed in the backtest path only
/// and always contribute 0 live, so recording them here would just be recording zeros). All-optional
/// and appended at the end so existing persisted predictions.json entries deserialize cleanly with
/// nulls, the same backward-compatibility approach QuantScoreCalculator.Weights uses for new fields.</summary>
public sealed record LoggedPrediction(
    string Ticker,
    DateTime LoggedAtUtc,
    double Price,
    double BenchmarkPrice,
    double Score,
    Signal Signal,
    double? ExcessReturnPct,
    bool? Hit,
    DateTime? EvaluatedAtUtc,
    double? TrendScore = null,
    double? MomentumScore = null,
    double? MacdScore = null,
    double? VolScore = null,
    double? MeanReversionScore = null,
    double? PriceMomentumScore = null,
    double? SentimentContrib = null);

/// <summary>One component's contribution to how much a ticker's logged score moved between the
/// earliest and latest logged entries that both have breakdown data.</summary>
public sealed record ScoreComponentDelta(string Label, double Delta);

/// <summary>Explains a ticker's overall score movement across its tracked history, not just that it
/// moved - SinceUtc is the earliest logged entry with breakdown data (the actual window this
/// explanation covers, which may be later than the ticker's very first logged entry if that entry
/// predates this field being recorded). TopDrivers is empty (not absent) when the components moved
/// but none individually enough to call out.</summary>
public sealed record ScoreChangeExplanation(double TotalDelta, DateTime SinceUtc, IReadOnlyList<ScoreComponentDelta> TopDrivers);

/// <summary>Forward-only companion to BacktestEngine: instead of validating against history, a live
/// prediction log records every real Quant Score a user has actually been shown, then checks back once
/// enough calendar time has passed to see whether the stock beat or lagged SPY - the same excess-return
/// label BacktestEngine uses, so the two are directly comparable. Because every entry is written before
/// its own outcome exists, this log can't suffer the lookahead or survivorship bias a historical
/// backtest is always at some risk of.</summary>
public static class PredictionLog
{
    /// <summary>Aggregates evaluated entries (ExcessReturnPct not null - unevaluated ones are silently
    /// skipped, so callers don't need to pre-filter) into the same Signal-bucketed shape
    /// BacktestEngine.BucketBySignal produces, so the live and historical tables read identically.</summary>
    public static IReadOnlyList<SignalStats> ComputeStats(IReadOnlyList<LoggedPrediction> entries)
    {
        var buckets = new Dictionary<Signal, List<LoggedPrediction>>
        {
            [Signal.Buy] = [], [Signal.Hold] = [], [Signal.Avoid] = []
        };
        foreach (var e in entries)
            if (e.ExcessReturnPct is not null) buckets[e.Signal].Add(e);

        return buckets.Select(kv =>
        {
            var (signal, items) = (kv.Key, kv.Value);
            var avg = items.Count > 0 ? items.Average(i => i.ExcessReturnPct!.Value) : 0.0;
            double? hitRate = signal switch
            {
                Signal.Buy or Signal.Avoid when items.Count > 0 => items.Count(i => i.Hit == true) / (double)items.Count * 100,
                _ => null
            };
            return new SignalStats(signal, items.Count, avg, hitRate);
        }).OrderBy(s => s.Signal).ToList();
    }

    /// <summary>Compares the earliest and latest entries (for one ticker - callers must pre-filter,
    /// same convention as ComputeStats taking pre-scoped entries) that carry component breakdown data,
    /// and ranks which components moved the most between them. Returns null rather than a
    /// zero/empty explanation when fewer than two such entries exist, since "why did it change" isn't
    /// answerable yet - not the same as "it didn't change" (that's TotalDelta ~ 0 with a real result).</summary>
    public static ScoreChangeExplanation? ExplainScoreChange(IReadOnlyList<LoggedPrediction> entries)
    {
        var withBreakdown = entries
            .Where(e => e.TrendScore is not null)
            .OrderBy(e => e.LoggedAtUtc)
            .ToList();
        if (withBreakdown.Count < 2) return null;

        var first = withBreakdown[0];
        var last = withBreakdown[^1];

        (string Label, double? FirstVal, double? LastVal)[] components =
        [
            ("Trend", first.TrendScore, last.TrendScore),
            ("Momentum", first.MomentumScore, last.MomentumScore),
            ("MACD", first.MacdScore, last.MacdScore),
            ("Volume", first.VolScore, last.VolScore),
            ("Mean Reversion", first.MeanReversionScore, last.MeanReversionScore),
            ("Short-Term Reversal", first.PriceMomentumScore, last.PriceMomentumScore),
            ("News Sentiment", first.SentimentContrib, last.SentimentContrib)
        ];

        var drivers = TopComponentDrivers(components);
        return new ScoreChangeExplanation(last.Score - first.Score, first.LoggedAtUtc, drivers);
    }

    /// <summary>Ranks which of a set of (label, before, after) component readings moved the most,
    /// filtering out essentially-unchanged ones - the shared rule behind ExplainScoreChange (one
    /// ticker's score over time) and UniverseRanking.ExplainTopNChanges (a ranked list's QuantScore
    /// component breakdown, month over month). Pulled out so both apply literally the same "what
    /// counts as a meaningful driver" definition instead of two independently-tuned copies.</summary>
    public static IReadOnlyList<ScoreComponentDelta> TopComponentDrivers(
        IReadOnlyList<(string Label, double? FirstVal, double? LastVal)> components, int take = 2, double minAbsDelta = 0.05) =>
        components
            .Where(c => c.FirstVal is not null && c.LastVal is not null)
            .Select(c => new ScoreComponentDelta(c.Label, c.LastVal!.Value - c.FirstVal!.Value))
            .Where(d => Math.Abs(d.Delta) > minAbsDelta)
            .OrderByDescending(d => Math.Abs(d.Delta))
            .Take(take)
            .ToList();
}
