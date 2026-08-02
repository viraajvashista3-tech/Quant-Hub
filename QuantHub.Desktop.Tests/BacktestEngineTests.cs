using QuantHub.Core.Analysis;
using QuantHub.Core.Backtesting;
using QuantHub.Core.Models;

namespace QuantHub.Desktop.Tests;

public class BacktestEngineTests
{
    [Fact]
    public void ChunkChronologically_CoversWholeInputWithNoGapsOrOverlaps()
    {
        var items = Enumerable.Range(0, 10).ToList();
        var chunks = BacktestEngine.ChunkChronologically(items, 4);

        Assert.Equal(4, chunks.Length);
        Assert.Equal(items, chunks.SelectMany(c => c).ToList()); // order preserved, nothing dropped/duplicated
    }

    [Fact]
    public void ChunkChronologically_EvenSplit_ProducesEqualSizedChunks()
    {
        var items = Enumerable.Range(0, 8).ToList();
        var chunks = BacktestEngine.ChunkChronologically(items, 4);

        Assert.All(chunks, c => Assert.Equal(2, c.Count));
    }

    [Fact]
    public void ChunkChronologically_UnevenSplit_StillCoversEverySample()
    {
        var items = Enumerable.Range(0, 10).ToList();
        var chunks = BacktestEngine.ChunkChronologically(items, 4);

        Assert.Equal(10, chunks.Sum(c => c.Count));
    }

    [Fact]
    public void ChunkChronologically_EmptyInput_ReturnsEmptyChunksNotNull()
    {
        var chunks = BacktestEngine.ChunkChronologically(new List<int>(), 4);

        Assert.Equal(4, chunks.Length);
        Assert.All(chunks, Assert.Empty);
    }

    [Fact]
    public void MergeSignalStats_WeightsAverageReturnAndHitRateByCount()
    {
        List<SignalStats> fold1 = [new(Signal.Buy, 10, 2.0, 60.0), new(Signal.Hold, 0, 0.0, null), new(Signal.Avoid, 0, 0.0, null)];
        List<SignalStats> fold2 = [new(Signal.Buy, 30, -1.0, 40.0), new(Signal.Hold, 0, 0.0, null), new(Signal.Avoid, 0, 0.0, null)];

        var merged = BacktestEngine.MergeSignalStats([fold1, fold2]);
        var buy = merged.Single(s => s.Signal == Signal.Buy);

        Assert.Equal(40, buy.Count);
        Assert.Equal((10 * 2.0 + 30 * -1.0) / 40, buy.AvgExcessReturnPct, 6);
        Assert.Equal((10 * 60.0 + 30 * 40.0) / 40, buy.HitRatePct!.Value, 6);
    }

    [Fact]
    public void MergeSignalStats_AllFoldsZeroCountForABucket_HitRateStaysNullNotZero()
    {
        List<SignalStats> fold1 = [new(Signal.Avoid, 0, 0.0, null)];
        List<SignalStats> fold2 = [new(Signal.Avoid, 0, 0.0, null)];

        var merged = BacktestEngine.MergeSignalStats([fold1, fold2]);
        var avoid = merged.Single(s => s.Signal == Signal.Avoid);

        Assert.Equal(0, avoid.Count);
        Assert.Null(avoid.HitRatePct);
    }

    [Fact]
    public void ExcessReturnPct_StockBeatsBenchmark_ReturnsPositiveExcess()
    {
        // Stock +10%, benchmark +4% -> beat the market by 6 points, not a raw +10% "win".
        var excess = BacktestEngine.ExcessReturnPct(closeNow: 100, closeFuture: 110, benchNow: 100, benchFuture: 104);
        Assert.Equal(6.0, excess, 9);
    }

    [Fact]
    public void ExcessReturnPct_StockUpButLagsBenchmark_ReturnsNegativeExcess()
    {
        // Stock +2% while the market is +8%: made money in absolute terms, but a real underperformer -
        // this is exactly the case a raw-return label would misclassify as a "win".
        var excess = BacktestEngine.ExcessReturnPct(closeNow: 50, closeFuture: 51, benchNow: 100, benchFuture: 108);
        Assert.Equal(-6.0, excess, 9);
    }

    [Fact]
    public void ExcessReturnPct_BothFlat_ReturnsZero()
    {
        var excess = BacktestEngine.ExcessReturnPct(closeNow: 100, closeFuture: 100, benchNow: 500, benchFuture: 500);
        Assert.Equal(0.0, excess, 9);
    }

    [Fact]
    public void PearsonCorrelation_PerfectlyCorrelated_ReturnsOne()
    {
        double[] x = [1, 2, 3, 4, 5];
        double[] y = [2, 4, 6, 8, 10];
        Assert.Equal(1.0, BacktestEngine.PearsonCorrelation(x, y), 9);
    }

    [Fact]
    public void PearsonCorrelation_PerfectlyAntiCorrelated_ReturnsMinusOne()
    {
        double[] x = [1, 2, 3, 4, 5];
        double[] y = [10, 8, 6, 4, 2];
        Assert.Equal(-1.0, BacktestEngine.PearsonCorrelation(x, y), 9);
    }

    [Fact]
    public void PearsonCorrelation_ConstantInput_ReturnsZeroNotNaN()
    {
        double[] x = [5, 5, 5, 5];
        double[] y = [1, 2, 3, 4];
        Assert.Equal(0.0, BacktestEngine.PearsonCorrelation(x, y));
    }

    [Fact]
    public void PearsonCorrelation_FewerThanTwoPoints_ReturnsZero()
    {
        Assert.Equal(0.0, BacktestEngine.PearsonCorrelation([1.0], [1.0]));
        Assert.Equal(0.0, BacktestEngine.PearsonCorrelation([], []));
    }

    [Fact]
    public void RecalibrateWeights_EqualMagnitudes_BoostsSmallerRangeComponentsMore()
    {
        // All nine components equally predictive -> each gets an equal 1/9 share of the 150-point
        // budget (25+15+15+10+15+20+20+15+15), meaning Vol (natural max 10) gets boosted the most and
        // Trend (natural max 25) gets scaled down the most. Works identically whether the inputs are
        // correlations or regression coefficients - RecalibrateWeights only looks at magnitude.
        var weights = BacktestEngine.RecalibrateWeights(0.2, 0.2, 0.2, 0.2, 0.2, 0.2, 0.2, 0.2, 0.2);
        var share = 150.0 / 9;

        Assert.Equal(share / 25.0, weights.Trend, 6);
        Assert.Equal(share / 15.0, weights.Momentum, 6);
        Assert.Equal(share / 15.0, weights.Macd, 6);
        Assert.Equal(share / 10.0, weights.Vol, 6);
        Assert.Equal(share / 15.0, weights.MeanReversion, 6);
        Assert.Equal(share / 20.0, weights.PriceMomentum, 6);
        Assert.Equal(share / 20.0, weights.RelativeStrength, 6);
        Assert.Equal(share / 15.0, weights.InsiderPurchase, 6);
        Assert.Equal(share / 15.0, weights.EarningsSurprise, 6);
    }

    [Fact]
    public void RecalibrateWeights_OneDominantComponent_TakesMostOfTheBudget()
    {
        var weights = BacktestEngine.RecalibrateWeights(0.8, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);

        Assert.Equal(150.0 / 25.0, weights.Trend, 6); // gets the entire budget
        Assert.Equal(0.0, weights.Momentum);
        Assert.Equal(0.0, weights.Macd);
        Assert.Equal(0.0, weights.Vol);
        Assert.Equal(0.0, weights.MeanReversion);
        Assert.Equal(0.0, weights.PriceMomentum);
        Assert.Equal(0.0, weights.RelativeStrength);
        Assert.Equal(0.0, weights.InsiderPurchase);
        Assert.Equal(0.0, weights.EarningsSurprise);
    }

    [Fact]
    public void RecalibrateWeights_NegativeValue_UsesMagnitudeNotSign()
    {
        // A strongly negative coefficient/correlation is just as "predictive" in magnitude as a
        // strongly positive one - recalibration weights by |value|, it doesn't flip the component's sign.
        var negative = BacktestEngine.RecalibrateWeights(-0.6, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
        var positive = BacktestEngine.RecalibrateWeights(0.6, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
        Assert.Equal(positive.Trend, negative.Trend, 9);
    }

    [Fact]
    public void RecalibrateWeights_NoSignalAtAll_FallsBackToDefaults()
    {
        var weights = BacktestEngine.RecalibrateWeights(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
        Assert.Equal(1.0, weights.Trend);
        Assert.Equal(1.0, weights.Momentum);
        Assert.Equal(1.0, weights.Macd);
        Assert.Equal(1.0, weights.Vol);
        Assert.Equal(1.0, weights.MeanReversion);
        Assert.Equal(1.0, weights.PriceMomentum);
        Assert.Equal(1.0, weights.RelativeStrength);
        Assert.Equal(1.0, weights.InsiderPurchase);
        Assert.Equal(1.0, weights.EarningsSurprise);
    }

    [Fact]
    public void BucketBySignal_PartitionsByThreshold_MatchingQuantScoreCalculatorRules()
    {
        double[] scores = [20, 15, -9, -10, 30, -25];
        double[] returns = [5.0, 1.0, -2.0, -8.0, 3.0, -6.0];

        var stats = BacktestEngine.BucketBySignal(scores, returns);

        var buy = stats.Single(s => s.Signal == Signal.Buy);
        var hold = stats.Single(s => s.Signal == Signal.Hold);
        var avoid = stats.Single(s => s.Signal == Signal.Avoid);

        Assert.Equal(2, buy.Count); // 20 and 30 (> BuyThreshold 15)
        Assert.Equal(2, hold.Count); // 15 and -9 (> HoldThreshold -10, not > 15)
        Assert.Equal(2, avoid.Count); // -10 and -25 (not > -10)
    }

    [Fact]
    public void BucketBySignal_HitRate_BuyCountsPositiveReturns_AvoidCountsNegativeReturns()
    {
        double[] scores = [20, 20, 20, -25, -25]; // 3x Buy, 2x Avoid
        double[] returns = [5.0, -1.0, 2.0, -3.0, 1.0]; // Buy: 2/3 positive; Avoid: 1/2 negative

        var stats = BacktestEngine.BucketBySignal(scores, returns);

        var buy = stats.Single(s => s.Signal == Signal.Buy);
        var avoid = stats.Single(s => s.Signal == Signal.Avoid);

        Assert.Equal(2.0 / 3.0 * 100, buy.HitRatePct!.Value, 6);
        Assert.Equal(50.0, avoid.HitRatePct!.Value, 6);
    }

    [Fact]
    public void BucketBySignal_HoldBucket_HasNoHitRateConcept()
    {
        double[] scores = [0.0, 5.0];
        double[] returns = [1.0, -1.0];

        var stats = BacktestEngine.BucketBySignal(scores, returns);
        var hold = stats.Single(s => s.Signal == Signal.Hold);

        Assert.Null(hold.HitRatePct);
    }

    [Fact]
    public void BucketBySignal_EmptyBucket_AvgReturnIsZeroNotNaN()
    {
        double[] scores = [30, 30]; // all Buy, nothing in Hold/Avoid
        double[] returns = [1.0, 2.0];

        var stats = BacktestEngine.BucketBySignal(scores, returns);
        var avoid = stats.Single(s => s.Signal == Signal.Avoid);

        Assert.Equal(0, avoid.Count);
        Assert.Equal(0.0, avoid.AvgExcessReturnPct);
        Assert.Null(avoid.HitRatePct);
    }

    [Fact]
    public void AverageWeights_MultipleHorizons_TakesElementwiseMean()
    {
        // Simulates the actual use case: each canonical horizon fits its own weights (which can
        // disagree, even in sign, per real backtest findings) and gets pooled into one stable set.
        var fiveDay = new QuantScoreCalculator.Weights(Trend: 2.0, Momentum: 0.0);
        var tenDay = new QuantScoreCalculator.Weights(Trend: 0.0, Momentum: 2.0);
        var twentyDay = new QuantScoreCalculator.Weights(Trend: 1.0, Momentum: 1.0);

        var avg = BacktestEngine.AverageWeights([fiveDay, tenDay, twentyDay]);

        Assert.Equal(1.0, avg.Trend, 9);
        Assert.Equal(1.0, avg.Momentum, 9);
        Assert.Equal(1.0, avg.Macd, 9); // untouched fields default to 1.0 in every input, mean stays 1.0
    }

    [Fact]
    public void AverageWeights_SingleInput_ReturnsItUnchanged()
    {
        var only = new QuantScoreCalculator.Weights(Trend: 1.7, Vol: 0.3);
        var avg = BacktestEngine.AverageWeights([only]);

        Assert.Equal(only, avg);
    }

    [Fact]
    public void AverageWeights_EmptyInput_FallsBackToDefaults()
    {
        var avg = BacktestEngine.AverageWeights([]);
        Assert.Equal(QuantScoreCalculator.Weights.Default, avg);
    }
}
