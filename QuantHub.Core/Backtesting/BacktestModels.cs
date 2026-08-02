using QuantHub.Core.Analysis;
using QuantHub.Core.Models;

namespace QuantHub.Core.Backtesting;

/// <summary>One backtestable component's historical correlation with excess (vs
/// BacktestEngine.BenchmarkTicker) forward returns, and the weight recalibration derived from it.</summary>
public sealed record ComponentStat(
    string Name,
    double Correlation,
    double CurrentMaxMagnitude,
    double CurrentWeight,
    double RecalibratedWeight);

/// <summary>Aggregate outcome for one Signal bucket (Buy/Hold/Avoid) under a given weight scheme -
/// how many historical samples fell into it, what they actually did over the forward horizon relative
/// to <see cref="BacktestEngine.BenchmarkTicker"/> (SPY) - not the stock's raw return - and (for
/// Buy/Avoid only) how often the directional call was right. Measuring against the benchmark rather
/// than in absolute terms means "hit" means "beat/lagged the market", so a rising hit rate can't just
/// be the market's own upward drift over longer horizons leaking into the number.</summary>
public sealed record SignalStats(
    Signal Signal,
    int Count,
    double AvgExcessReturnPct,
    double? HitRatePct);

/// <summary>
/// CurrentSignalStats/RecalibratedSignalStats are out-of-sample: aggregated across
/// <see cref="WalkForwardSteps"/> expanding-window folds, where each fold's weights (recalibrated
/// or not) were evaluated only on a chunk of data that came chronologically after its training
/// window - never on data used to fit them, and always evaluated against HorizonTradingDays' excess
/// (vs BenchmarkTicker) return. RecalibratedWeights (the ones Apply persists) are a separate final fit
/// using each of BacktestEngine.CanonicalHorizons' entire datasets, averaged together - not just a fit
/// on HorizonTradingDays alone - so the weights that actually reach QuantScoreCalculator are pooled
/// across look-ahead horizons instead of overfit to whichever one is currently selected in the UI.
/// </summary>
public sealed record BacktestReport(
    IReadOnlyList<ComponentStat> Components,
    IReadOnlyList<SignalStats> CurrentSignalStats,
    IReadOnlyList<SignalStats> RecalibratedSignalStats,
    QuantScoreCalculator.Weights RecalibratedWeights,
    int SampleCount,
    int OutOfSampleCount,
    int WalkForwardSteps,
    int TickerCount,
    IReadOnlyList<string> SkippedTickers,
    int HorizonTradingDays,
    DateTime RanAtUtc);
