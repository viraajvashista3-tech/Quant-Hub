using QuantHub.Core.Backtesting;
using QuantHub.Core.Models;

namespace QuantHub.Desktop.Tests;

public class PredictionLogTests
{
    private static LoggedPrediction Evaluated(Signal signal, double excessReturnPct, bool hit) =>
        new("TST", DateTime.UtcNow.AddDays(-20), 100, 500, 10, signal, excessReturnPct, hit, DateTime.UtcNow);

    private static LoggedPrediction Unevaluated(Signal signal) =>
        new("TST", DateTime.UtcNow, 100, 500, 10, signal, null, null, null);

    [Fact]
    public void ComputeStats_BuyHitRate_CountsPositiveExcessReturnsAsHits()
    {
        List<LoggedPrediction> entries =
        [
            Evaluated(Signal.Buy, 5.0, true),
            Evaluated(Signal.Buy, -2.0, false),
            Evaluated(Signal.Buy, 3.0, true)
        ];

        var stats = PredictionLog.ComputeStats(entries);
        var buy = stats.Single(s => s.Signal == Signal.Buy);

        Assert.Equal(3, buy.Count);
        Assert.Equal(2.0 / 3.0 * 100, buy.HitRatePct!.Value, 6);
        Assert.Equal((5.0 - 2.0 + 3.0) / 3.0, buy.AvgExcessReturnPct, 9);
    }

    [Fact]
    public void ComputeStats_AvoidHitRate_CountsNegativeExcessReturnsAsHits()
    {
        List<LoggedPrediction> entries =
        [
            Evaluated(Signal.Avoid, -4.0, true),
            Evaluated(Signal.Avoid, 1.0, false)
        ];

        var stats = PredictionLog.ComputeStats(entries);
        var avoid = stats.Single(s => s.Signal == Signal.Avoid);

        Assert.Equal(2, avoid.Count);
        Assert.Equal(50.0, avoid.HitRatePct!.Value, 6);
    }

    [Fact]
    public void ComputeStats_HoldBucket_HasNoHitRateConcept()
    {
        List<LoggedPrediction> entries = [Evaluated(Signal.Hold, 1.0, false)];

        var stats = PredictionLog.ComputeStats(entries);
        var hold = stats.Single(s => s.Signal == Signal.Hold);

        Assert.Null(hold.HitRatePct);
    }

    [Fact]
    public void ComputeStats_UnevaluatedEntries_AreExcludedNotCountedAsMisses()
    {
        List<LoggedPrediction> entries =
        [
            Evaluated(Signal.Buy, 5.0, true),
            Unevaluated(Signal.Buy),
            Unevaluated(Signal.Avoid)
        ];

        var stats = PredictionLog.ComputeStats(entries);
        var buy = stats.Single(s => s.Signal == Signal.Buy);
        var avoid = stats.Single(s => s.Signal == Signal.Avoid);

        Assert.Equal(1, buy.Count); // the unevaluated Buy entry doesn't count
        Assert.Equal(0, avoid.Count);
        Assert.Null(avoid.HitRatePct);
    }

    [Fact]
    public void ComputeStats_EmptyInput_ReturnsAllThreeBucketsWithZeroCount()
    {
        var stats = PredictionLog.ComputeStats([]);

        Assert.Equal(3, stats.Count);
        Assert.All(stats, s => Assert.Equal(0, s.Count));
        Assert.All(stats, s => Assert.Equal(0.0, s.AvgExcessReturnPct));
    }

    [Fact]
    public void ComputeStats_EmptyBucket_AvgReturnIsZeroNotNaN()
    {
        List<LoggedPrediction> entries = [Evaluated(Signal.Buy, 5.0, true)];

        var stats = PredictionLog.ComputeStats(entries);
        var avoid = stats.Single(s => s.Signal == Signal.Avoid);

        Assert.Equal(0, avoid.Count);
        Assert.Equal(0.0, avoid.AvgExcessReturnPct);
        Assert.Null(avoid.HitRatePct);
    }

    // ---------- ExplainScoreChange ----------

    private static LoggedPrediction WithBreakdown(
        DateTime loggedAtUtc, double score,
        double trend, double momentum, double macd, double vol, double meanReversion, double priceMomentum, double sentiment) =>
        new("TST", loggedAtUtc, 100, 500, score, Signal.Hold, null, null, null,
            trend, momentum, macd, vol, meanReversion, priceMomentum, sentiment);

    [Fact]
    public void ExplainScoreChange_FewerThanTwoEntriesWithBreakdown_ReturnsNull()
    {
        List<LoggedPrediction> oneEntry = [WithBreakdown(DateTime.UtcNow, 10, 1, 1, 1, 1, 1, 1, 1)];
        Assert.Null(PredictionLog.ExplainScoreChange(oneEntry));

        // A legacy entry (predates the breakdown fields, all null) doesn't count even alongside one real entry.
        List<LoggedPrediction> oneRealOneLegacy =
        [
            new("TST", DateTime.UtcNow.AddDays(-5), 100, 500, 8, Signal.Hold, null, null, null),
            WithBreakdown(DateTime.UtcNow, 10, 1, 1, 1, 1, 1, 1, 1)
        ];
        Assert.Null(PredictionLog.ExplainScoreChange(oneRealOneLegacy));
    }

    [Fact]
    public void ExplainScoreChange_UsesEarliestAndLatestEntriesWithBreakdownData_IgnoringOrder()
    {
        var day1 = DateTime.UtcNow.AddDays(-10);
        var day2 = DateTime.UtcNow.AddDays(-5);
        var day3 = DateTime.UtcNow;

        // Passed out of chronological order - ExplainScoreChange must sort by LoggedAtUtc itself.
        List<LoggedPrediction> entries =
        [
            WithBreakdown(day3, 25, 10, 5, 5, 2, 1, 1, 1),
            WithBreakdown(day1, 10, 2, 2, 2, 2, 1, 0, 1),
            WithBreakdown(day2, 18, 6, 3, 3, 2, 1, 0.5, 1)
        ];

        var result = PredictionLog.ExplainScoreChange(entries)!;

        Assert.Equal(15.0, result.TotalDelta, 6); // 25 (day3) - 10 (day1)
        Assert.Equal(day1, result.SinceUtc);
    }

    [Fact]
    public void ExplainScoreChange_RanksTopTwoDriversByAbsoluteMagnitude()
    {
        var first = WithBreakdown(DateTime.UtcNow.AddDays(-10), 10,
            trend: 0, momentum: 0, macd: 0, vol: 0, meanReversion: 0, priceMomentum: 0, sentiment: 0);
        var last = WithBreakdown(DateTime.UtcNow, 30,
            trend: 12, momentum: -8, macd: 1, vol: 0.5, meanReversion: 0, priceMomentum: 0, sentiment: 3);

        var result = PredictionLog.ExplainScoreChange([first, last])!;

        Assert.Equal(2, result.TopDrivers.Count);
        Assert.Equal("Trend", result.TopDrivers[0].Label);
        Assert.Equal(12.0, result.TopDrivers[0].Delta, 6);
        Assert.Equal("Momentum", result.TopDrivers[1].Label);
        Assert.Equal(-8.0, result.TopDrivers[1].Delta, 6);
    }

    [Fact]
    public void ExplainScoreChange_TinyComponentMoves_AreExcludedFromDrivers()
    {
        var first = WithBreakdown(DateTime.UtcNow.AddDays(-10), 10, 0, 0, 0, 0, 0, 0, 0);
        var last = WithBreakdown(DateTime.UtcNow, 10.02, 0.02, 0, 0, 0, 0, 0, 0);

        var result = PredictionLog.ExplainScoreChange([first, last])!;

        Assert.Empty(result.TopDrivers);
    }
}
