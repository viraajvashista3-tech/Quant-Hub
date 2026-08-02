using QuantHub.Core.Analysis;
using QuantHub.Core.Models;

namespace QuantHub.Desktop.Tests;

public class QuantScoreCalculatorTests
{
    // ---------- TrendSignal ----------

    [Fact]
    public void TrendSignal_PriceFarAboveBothMAs_MaxesAtOne()
    {
        // 30% above MA200 (cap 20%) and ~13% above MA50 (cap 10%) - both clamp to +1.
        Assert.Equal(1.0, QuantScoreCalculator.TrendSignal(130, 115, 100), 6);
    }

    [Fact]
    public void TrendSignal_PriceFarBelowBothMAs_MaxesAtMinusOne()
    {
        Assert.Equal(-1.0, QuantScoreCalculator.TrendSignal(70, 85, 100), 6);
    }

    [Fact]
    public void TrendSignal_BlendsMa200At60PercentAndMa50At40Percent()
    {
        // 10% above MA200 -> component 0.5 (not maxed); exactly 10% above MA50 -> component 1.0 (maxed)
        var result = QuantScoreCalculator.TrendSignal(110, 100, 100);
        Assert.Equal(0.6 * 0.5 + 0.4 * 1.0, result, 6);
    }

    [Fact]
    public void TrendSignal_OnlyMa200Available_UsesThatComponentAlone()
    {
        Assert.Equal(1.0, QuantScoreCalculator.TrendSignal(120, null, 100), 6);
    }

    [Fact]
    public void TrendSignal_OnlyMa50Available_UsesThatComponentAlone()
    {
        Assert.Equal(1.0, QuantScoreCalculator.TrendSignal(110, 100, null), 6);
    }

    [Fact]
    public void TrendSignal_BothMasMissing_ReturnsZeroNeutral()
    {
        Assert.Equal(0.0, QuantScoreCalculator.TrendSignal(100, null, null));
    }

    // ---------- MomentumSignal ----------

    [Theory]
    [InlineData(80, 1.0)]
    [InlineData(20, -1.0)]
    [InlineData(65, 0.5)]
    [InlineData(50, 0.0)]
    [InlineData(95, 1.0)] // past the cap, clamps rather than exceeding 1
    public void MomentumSignal_ScalesRsiAroundNeutral50(double rsi, double expected)
    {
        Assert.Equal(expected, QuantScoreCalculator.MomentumSignal(rsi), 6);
    }

    [Fact]
    public void MomentumSignal_MissingRsi_DefaultsToNeutral50()
    {
        Assert.Equal(0.0, QuantScoreCalculator.MomentumSignal(null));
    }

    // ---------- MacdSignal ----------

    [Fact]
    public void MacdSignal_NormalizesByThreePercentOfPrice()
    {
        Assert.Equal(1.0 / 3.0, QuantScoreCalculator.MacdSignal(1, 0, 100), 6);
    }

    [Fact]
    public void MacdSignal_ExtremeSpread_ClampsToOne()
    {
        Assert.Equal(1.0, QuantScoreCalculator.MacdSignal(10, 0, 100), 6);
    }

    [Fact]
    public void MacdSignal_MissingInputs_ReturnsZero()
    {
        Assert.Equal(0.0, QuantScoreCalculator.MacdSignal(null, 0, 100));
        Assert.Equal(0.0, QuantScoreCalculator.MacdSignal(1, null, 100));
        Assert.Equal(0.0, QuantScoreCalculator.MacdSignal(1, 0, 0));
    }

    // ---------- VolumeSignal ----------

    [Theory]
    [InlineData(2.0, 1.0)]
    [InlineData(0.0, -1.0)]
    [InlineData(1.5, 0.5)]
    [InlineData(1.0, 0.0)]
    [InlineData(3.0, 1.0)] // past the cap
    public void VolumeSignal_CentersOnNormalRatioOfOne(double ratio, double expected)
    {
        Assert.Equal(expected, QuantScoreCalculator.VolumeSignal(ratio), 6);
    }

    // ---------- MeanReversionSignal (Bollinger %B, inverted) ----------

    [Fact]
    public void MeanReversionSignal_AtLowerBand_IsMaximallyBullish()
    {
        Assert.Equal(1.0, QuantScoreCalculator.MeanReversionSignal(90, 110, 90), 6);
    }

    [Fact]
    public void MeanReversionSignal_AtUpperBand_IsMaximallyBearish()
    {
        Assert.Equal(-1.0, QuantScoreCalculator.MeanReversionSignal(110, 110, 90), 6);
    }

    [Fact]
    public void MeanReversionSignal_AtMidBand_IsNeutral()
    {
        Assert.Equal(0.0, QuantScoreCalculator.MeanReversionSignal(100, 110, 90), 6);
    }

    [Fact]
    public void MeanReversionSignal_BeyondLowerBand_ClampsRatherThanExceedingOne()
    {
        Assert.Equal(1.0, QuantScoreCalculator.MeanReversionSignal(80, 110, 90), 6);
    }

    [Fact]
    public void MeanReversionSignal_DegenerateOrMissingBands_ReturnsZero()
    {
        Assert.Equal(0.0, QuantScoreCalculator.MeanReversionSignal(100, null, 90));
        Assert.Equal(0.0, QuantScoreCalculator.MeanReversionSignal(100, 90, null));
        Assert.Equal(0.0, QuantScoreCalculator.MeanReversionSignal(100, 90, 90)); // upper <= lower
    }

    // ---------- PriceMomentumSignal (21-day ROC, inverted - see doc comment: backtesting showed
    // this specific 1-month window captures short-term reversal, not momentum continuation) ----------

    [Theory]
    [InlineData(15, -1.0)]  // big recent run-up -> negative (expect reversal, not continuation)
    [InlineData(-15, 1.0)]  // big recent drop -> positive (expect a bounce)
    [InlineData(7.5, -0.5)]
    [InlineData(30, -1.0)] // past the cap
    public void PriceMomentumSignal_ScalesRocWithFifteenPercentCap_AndInvertsSign(double roc, double expected)
    {
        Assert.Equal(expected, QuantScoreCalculator.PriceMomentumSignal(roc), 6);
    }

    [Fact]
    public void PriceMomentumSignal_MissingRoc_ReturnsZero()
    {
        Assert.Equal(0.0, QuantScoreCalculator.PriceMomentumSignal(null));
    }

    // ---------- RelativeStrengthSignal (excess return vs sector peers) ----------

    [Theory]
    [InlineData(10, 1.0)]   // beat peers by 10pp -> maxed positive
    [InlineData(-10, -1.0)] // lagged peers by 10pp -> maxed negative
    [InlineData(5, 0.5)]
    [InlineData(20, 1.0)]   // past the cap
    public void RelativeStrengthSignal_ScalesExcessReturnWithTenPointCap(double excess, double expected)
    {
        Assert.Equal(expected, QuantScoreCalculator.RelativeStrengthSignal(excess), 6);
    }

    [Fact]
    public void RelativeStrengthSignal_MissingExcessReturn_ReturnsZero()
    {
        Assert.Equal(0.0, QuantScoreCalculator.RelativeStrengthSignal(null));
    }

    // ---------- InsiderPurchaseSignal (decays from a Form-4 Purchase filing, no Sale side) ----------

    [Fact]
    public void InsiderPurchaseSignal_FiledToday_IsMaximallyPositive()
    {
        Assert.Equal(1.0, QuantScoreCalculator.InsiderPurchaseSignal(0), 6);
    }

    [Fact]
    public void InsiderPurchaseSignal_DecaysLinearlyToZeroAtDecayWindow()
    {
        Assert.Equal(0.5, QuantScoreCalculator.InsiderPurchaseSignal(QuantScoreCalculator.InsiderPurchaseDecayDays / 2), 6);
        Assert.Equal(0.0, QuantScoreCalculator.InsiderPurchaseSignal(QuantScoreCalculator.InsiderPurchaseDecayDays), 6);
    }

    [Fact]
    public void InsiderPurchaseSignal_BeyondDecayWindow_ReturnsZeroNotNegative()
    {
        Assert.Equal(0.0, QuantScoreCalculator.InsiderPurchaseSignal(QuantScoreCalculator.InsiderPurchaseDecayDays + 100));
    }

    [Fact]
    public void InsiderPurchaseSignal_NoPurchaseOrNegativeDays_ReturnsZero()
    {
        Assert.Equal(0.0, QuantScoreCalculator.InsiderPurchaseSignal(null));
        Assert.Equal(0.0, QuantScoreCalculator.InsiderPurchaseSignal(-1)); // a filing "in the future" relative to the bar - shouldn't happen causally, but must not go negative
    }

    // ---------- EarningsSurpriseSignal (decays from the most recent reported quarter's EPS surprise%,
    // continuation not reversal - see doc comment: PEAD is a different, well-replicated effect from
    // PriceMomentumSignal's short-term reversal) ----------

    [Fact]
    public void EarningsSurpriseSignal_ReportedToday_ScalesSurpriseWithTenPercentCap()
    {
        Assert.Equal(1.0, QuantScoreCalculator.EarningsSurpriseSignal(15, 0), 6); // past the cap, clamps
        Assert.Equal(0.5, QuantScoreCalculator.EarningsSurpriseSignal(5, 0), 6);
        Assert.Equal(-1.0, QuantScoreCalculator.EarningsSurpriseSignal(-15, 0), 6); // a miss scores negative, not inverted
    }

    [Fact]
    public void EarningsSurpriseSignal_DecaysLinearlyToZeroAtDecayWindow()
    {
        var half = QuantScoreCalculator.EarningsSurpriseDecayDays / 2;
        Assert.Equal(0.5, QuantScoreCalculator.EarningsSurpriseSignal(10, half), 6);
        Assert.Equal(0.0, QuantScoreCalculator.EarningsSurpriseSignal(10, QuantScoreCalculator.EarningsSurpriseDecayDays), 6);
    }

    [Fact]
    public void EarningsSurpriseSignal_BeyondDecayWindow_ReturnsZero()
    {
        Assert.Equal(0.0, QuantScoreCalculator.EarningsSurpriseSignal(10, QuantScoreCalculator.EarningsSurpriseDecayDays + 30));
    }

    [Fact]
    public void EarningsSurpriseSignal_MissingSurpriseOrDays_ReturnsZero()
    {
        Assert.Equal(0.0, QuantScoreCalculator.EarningsSurpriseSignal(null, 0));
        Assert.Equal(0.0, QuantScoreCalculator.EarningsSurpriseSignal(10, null));
        Assert.Equal(0.0, QuantScoreCalculator.EarningsSurpriseSignal(10, -1)); // "in the future" relative to the bar - shouldn't happen causally, but must not go negative-days
    }

    // ---------- AboveMa / IsGoldenCross (explanatory booleans, decoupled from the score) ----------

    [Fact]
    public void AboveMa_ComparesCloseToMovingAverage()
    {
        Assert.True(QuantScoreCalculator.AboveMa(100, 90));
        Assert.False(QuantScoreCalculator.AboveMa(100, 110));
        Assert.Null(QuantScoreCalculator.AboveMa(100, null));
    }

    [Fact]
    public void IsGoldenCross_ComparesMa50ToMa200()
    {
        Assert.True(QuantScoreCalculator.IsGoldenCross(100, 90));
        Assert.False(QuantScoreCalculator.IsGoldenCross(90, 100));
        Assert.Null(QuantScoreCalculator.IsGoldenCross(null, 100));
        Assert.Null(QuantScoreCalculator.IsGoldenCross(100, null));
    }

    // ---------- Calculate (full integration) ----------

    private static QuantScoreCalculator.Result Calc(
        double latestClose = 100, double? ma50 = null, double? ma200 = null, double? rsi = null,
        double? macd = null, double? signal = null, long latestVolume = 1000, IReadOnlyList<long>? volumes = null,
        long avgVolumeFull = 1000, double? bbUpper = null, double? bbLower = null, double? roc21Pct = null,
        double sentimentScore = 0.0, QuantScoreCalculator.Weights? weights = null, double sentimentWeight = 1.0,
        double? excessRoc21Pct = null, double? daysSinceLastInsiderPurchase = null,
        double? daysSinceLastEarnings = null, double? lastEarningsSurprisePercent = null) =>
        QuantScoreCalculator.Calculate(latestClose, ma50, ma200, rsi, macd, signal, latestVolume,
            volumes ?? [], avgVolumeFull, bbUpper, bbLower, roc21Pct, sentimentScore, weights, sentimentWeight,
            excessRoc21Pct, daysSinceLastInsiderPurchase, daysSinceLastEarnings, lastEarningsSurprisePercent);

    [Fact]
    public void Calculate_AllComponentsMaxedPositive_SumsToExpectedTotalAndBuySignal()
    {
        var result = Calc(
            latestClose: 130, ma50: 115, ma200: 100, // trend maxed +1
            rsi: 80,                                  // momentum maxed +1
            macd: 10, signal: 0,                      // macd maxed +1 (normalized by 3% of 130)
            latestVolume: 2000, volumes: [], avgVolumeFull: 1000, // vol ratio 2.0 -> maxed +1
            bbUpper: 140, bbLower: 120,                // close=130 is mid-band -> 0 (neutral)
            roc21Pct: -15,                             // sharp 1-month drop -> reversal signal maxed +1
            sentimentScore: 0.5);

        Assert.Equal(QuantScoreCalculator.TrendMax, result.TrendScore, 6);
        Assert.Equal(QuantScoreCalculator.MomentumMax, result.MomentumScore, 6);
        Assert.Equal(QuantScoreCalculator.MacdMax, result.MacdScore, 6);
        Assert.Equal(QuantScoreCalculator.VolMax, result.VolScore, 6);
        Assert.Equal(0.0, result.MeanReversionScore, 6);
        Assert.Equal(QuantScoreCalculator.PriceMomentumMax, result.PriceMomentumScore, 6);
        Assert.Equal(20.0, result.SentimentContrib, 6);

        var expectedTotal = QuantScoreCalculator.TrendMax + QuantScoreCalculator.MomentumMax
            + QuantScoreCalculator.MacdMax + QuantScoreCalculator.VolMax + 0.0
            + QuantScoreCalculator.PriceMomentumMax + 20.0;
        Assert.Equal(expectedTotal, result.QuantScore, 6);
        Assert.Equal(Signal.Buy, result.Signal);
    }

    [Fact]
    public void Calculate_MissingMovingAverages_TrendContributesZero_AboveMaAndCrossAreNull()
    {
        var result = Calc(ma50: null, ma200: null);
        Assert.Equal(0.0, result.TrendScore);
        Assert.Null(result.AboveMa50);
        Assert.Null(result.AboveMa200);
        Assert.Null(result.GoldenCross);
    }

    [Fact]
    public void Calculate_RecentInsiderPurchase_ContributesPositiveScore()
    {
        var withPurchase = Calc(daysSinceLastInsiderPurchase: 0);
        var noPurchase = Calc();

        Assert.Equal(QuantScoreCalculator.InsiderPurchaseMax, withPurchase.InsiderPurchaseScore, 6);
        Assert.Equal(0.0, noPurchase.InsiderPurchaseScore);
        Assert.Equal(withPurchase.InsiderPurchaseScore, withPurchase.QuantScore - noPurchase.QuantScore, 6);
    }

    [Fact]
    public void Calculate_RecentEarningsBeat_ContributesPositiveScore()
    {
        var withBeat = Calc(daysSinceLastEarnings: 0, lastEarningsSurprisePercent: 10);
        var noEarnings = Calc();

        Assert.Equal(QuantScoreCalculator.EarningsSurpriseMax, withBeat.EarningsSurpriseScore, 6);
        Assert.Equal(0.0, noEarnings.EarningsSurpriseScore);
        Assert.Equal(withBeat.EarningsSurpriseScore, withBeat.QuantScore - noEarnings.QuantScore, 6);
    }

    [Fact]
    public void Calculate_CustomWeights_ScaleTheirOwnComponentOnly()
    {
        var weights = new QuantScoreCalculator.Weights(Trend: 2.0, Momentum: 0.5);
        var unweighted = Calc(latestClose: 130, ma50: 115, ma200: 100, rsi: 80, sentimentScore: 0.1);
        var weighted = Calc(latestClose: 130, ma50: 115, ma200: 100, rsi: 80, sentimentScore: 0.1, weights: weights);

        Assert.Equal(unweighted.TrendScore * 2.0, weighted.TrendScore, 6);
        Assert.Equal(unweighted.MomentumScore * 0.5, weighted.MomentumScore, 6);
        Assert.Equal(unweighted.MacdScore, weighted.MacdScore, 6); // untouched components unaffected
        Assert.Equal(unweighted.SentimentContrib, weighted.SentimentContrib, 6); // sentiment never weighted here
    }

    [Fact]
    public void Calculate_SentimentWeight_ScalesSentimentContribOnly()
    {
        var baseline = Calc(latestClose: 130, ma50: 115, ma200: 100, sentimentScore: 0.5);
        var boosted = Calc(latestClose: 130, ma50: 115, ma200: 100, sentimentScore: 0.5, sentimentWeight: 1.5);

        Assert.Equal(20.0, baseline.SentimentContrib, 6);
        Assert.Equal(30.0, boosted.SentimentContrib, 6);
        Assert.Equal(baseline.TrendScore, boosted.TrendScore, 6);
    }

    [Theory]
    [InlineData(15.01, Signal.Buy)]
    [InlineData(15.0, Signal.Hold)]  // strict > required for Buy
    [InlineData(-9.99, Signal.Hold)]
    [InlineData(-10.0, Signal.Avoid)] // strict > required for Hold
    public void SignalThresholds_AreStrictInequalities(double targetScore, Signal expected)
    {
        // Isolate to sentiment alone (everything else neutral/zero) so we can hit an exact score.
        var result = Calc(latestClose: 100, sentimentScore: targetScore / 40.0);
        Assert.Equal(targetScore, result.QuantScore, 6);
        Assert.Equal(expected, result.Signal);
    }

    [Fact]
    public void VolumeRatio_UsesLast20BarsNotFullPeriodAverage_WhenEnoughBarsExist()
    {
        var volumes = Enumerable.Repeat(100L, 5).Concat(Enumerable.Repeat(50L, 20)).ToArray();
        var result = Calc(latestVolume: 75, volumes: volumes, avgVolumeFull: 60);

        Assert.Equal(1.5, result.VolRatio, 3);
    }

    [Fact]
    public void VolumeRatio_FallsBackToFullPeriodAverage_WhenFewerThan20Bars()
    {
        long[] volumes = [10, 20, 30];
        var result = Calc(latestVolume: 30, volumes: volumes, avgVolumeFull: 20);

        Assert.Equal(1.5, result.VolRatio, 3);
    }

    [Fact]
    public void VolumeRatio_ZeroAverageDefaultsToOne()
    {
        var result = Calc(latestVolume: 100, volumes: [], avgVolumeFull: 0);
        Assert.Equal(1.0, result.VolRatio);
    }
}
