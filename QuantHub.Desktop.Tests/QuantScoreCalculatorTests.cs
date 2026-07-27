using QuantHub.Core.Analysis;
using QuantHub.Core.Models;

namespace QuantHub.Desktop.Tests;

public class QuantScoreCalculatorTests
{
    private static QuantScoreCalculator.Result Calc(
        double latestClose = 100,
        double? ma50 = null,
        double? ma200 = null,
        double? rsi = null,
        double? macd = null,
        double? signal = null,
        long latestVolume = 1000,
        IReadOnlyList<long>? volumes = null,
        long avgVolumeFull = 1000,
        double sentimentScore = 0.0) =>
        QuantScoreCalculator.Calculate(latestClose, ma50, ma200, rsi, macd, signal, latestVolume,
            volumes ?? [], avgVolumeFull, sentimentScore);

    [Fact]
    public void MissingRsi_DefaultsTo50_FallsInMildBullishBand()
    {
        var result = Calc(rsi: null);
        Assert.Equal(10.0, result.MomentumScore);
    }

    [Theory]
    [InlineData(70, -10.0)]
    [InlineData(65, 20.0)]
    [InlineData(55, 10.0)]
    [InlineData(45, -5.0)]
    [InlineData(35, -15.0)]
    [InlineData(10, -20.0)]
    public void RsiBands_MatchExactThresholds(double rsi, double expectedMomentum)
    {
        var result = Calc(rsi: rsi);
        Assert.Equal(expectedMomentum, result.MomentumScore);
    }

    [Fact]
    public void MissingMacdAndSignal_TieGoesToBearish()
    {
        var result = Calc(macd: null, signal: null);
        Assert.Equal(-15.0, result.MacdScore);
    }

    [Fact]
    public void MacdAboveSignal_IsBullish()
    {
        var result = Calc(macd: 1.0, signal: 0.5);
        Assert.Equal(15.0, result.MacdScore);
    }

    [Fact]
    public void MissingMovingAverages_ContributeZeroToTrend()
    {
        var result = Calc(ma50: null, ma200: null);
        Assert.Equal(0.0, result.TrendScore);
        Assert.Null(result.AboveMa50);
        Assert.Null(result.AboveMa200);
        Assert.Null(result.GoldenCross);
    }

    [Fact]
    public void PriceAboveBothMovingAveragesWithGoldenCross_MaxTrendScore()
    {
        var result = Calc(latestClose: 100, ma50: 90, ma200: 80);
        // above MA200 (+15) + above MA50 (+10) + golden cross ma50>ma200 (+5)
        Assert.Equal(30.0, result.TrendScore);
        Assert.True(result.AboveMa50);
        Assert.True(result.AboveMa200);
        Assert.True(result.GoldenCross);
    }

    [Fact]
    public void PriceBelowBothMovingAveragesWithDeathCross_MinTrendScore()
    {
        var result = Calc(latestClose: 100, ma50: 110, ma200: 120);
        // below MA200 (-15) + below MA50 (-10) + death cross ma50<ma200 (-5)
        Assert.Equal(-30.0, result.TrendScore);
        Assert.False(result.AboveMa50);
        Assert.False(result.AboveMa200);
        Assert.False(result.GoldenCross);
    }

    [Fact]
    public void VolumeRatio_UsesLast20BarsNotFullPeriodAverage_WhenEnoughBarsExist()
    {
        // 25 bars: first 5 are noise (100), last 20 average to 50 - the 20-day tail average
        // should win over the full-period average that the display-only avgVolume field uses.
        var volumes = Enumerable.Repeat(100L, 5).Concat(Enumerable.Repeat(50L, 20)).ToArray();
        var result = Calc(latestVolume: 75, volumes: volumes, avgVolumeFull: 60);

        // 75 / 50 (20-day avg) = 1.5 -> vol_score 10, NOT 75/60=1.25 -> vol_score 5
        Assert.Equal(1.5, result.VolRatio, 3);
        Assert.Equal(10.0, result.VolScore);
    }

    [Fact]
    public void VolumeRatio_FallsBackToFullPeriodAverage_WhenFewerThan20Bars()
    {
        long[] volumes = [10, 20, 30];
        var result = Calc(latestVolume: 30, volumes: volumes, avgVolumeFull: 20);

        Assert.Equal(1.5, result.VolRatio, 3);
        Assert.Equal(10.0, result.VolScore);
    }

    [Fact]
    public void VolumeRatio_ZeroAverageDefaultsToOne()
    {
        var result = Calc(latestVolume: 100, volumes: [], avgVolumeFull: 0);
        Assert.Equal(1.0, result.VolRatio);
        Assert.Equal(5.0, result.VolScore);
    }

    [Fact]
    public void SentimentContrib_IsScoreTimesForty()
    {
        var result = Calc(sentimentScore: 0.5);
        Assert.Equal(20.0, result.SentimentContrib);
    }

    [Theory]
    [InlineData(21, Signal.Buy)]
    [InlineData(20, Signal.Hold)] // strict > 20 required for Buy
    [InlineData(-14, Signal.Hold)]
    [InlineData(-15, Signal.Avoid)] // strict > -15 required for Hold
    public void SignalThresholds_AreStrictInequalities(double targetScore, Signal expected)
    {
        // Isolate to sentiment alone (macd forced to a neutral offset) so we can hit an exact
        // quant score: trend=0, momentum with rsi=45->-5, macd tie->-15, volume with 0 avg->5.
        // baseline = 0 - 5 - 15 + 5 = -15; sentimentContrib = targetScore - baseline.
        var sentimentContrib = targetScore - (-15.0);
        var result = Calc(rsi: 45, macd: null, signal: null, volumes: [], avgVolumeFull: 0,
            sentimentScore: sentimentContrib / 40.0);

        Assert.Equal(targetScore, result.QuantScore, 6);
        Assert.Equal(expected, result.Signal);
    }
}
