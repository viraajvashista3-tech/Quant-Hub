using QuantHub.Core.Models;

namespace QuantHub.Core.Analysis;

/// <summary>
/// Quant Score v2 - a from-scratch rewrite of the original hand-picked, discretely-bucketed 5-point
/// scorer (Trend ±30 via 3 binary sub-signals, Momentum via 6 hard RSI bands, MACD binary ±15,
/// Volume via 3 buckets). Empirical walk-forward backtesting of that version (see BacktestEngine)
/// showed correlations with forward returns under 0.06 in magnitude for every component at every
/// horizon tested (5/10/20/60 trading days) - and Trend/Momentum got *more* negative as the horizon
/// lengthened, the opposite of what recalibrating weights alone can fix. That's a sign the discrete
/// buckets were throwing away real information (a stock 0.1% above its 200-day MA scored identically
/// to one 19% above it), not just mis-weighted.
///
/// This version:
/// 1. Makes every technical component continuous (a real-valued signal roughly in [-1, +1]) instead
///    of a step function, so magnitude of a move counts, not just its sign.
/// 2. Adds two components with actual academic support - Bollinger %B (mean reversion) and 21-day
///    rate of change (medium-term price momentum, the most replicated equity factor there is) -
///    replacing two of this app's already-computed-but-previously-unused indicators.
/// 3. Keeps the same "hand-picked point budget, recalibrated by BacktestEngine's walk-forward
///    correlation analysis" architecture, since that mechanism itself was sound - the inputs feeding
///    it were the problem.
///
/// None of this guarantees the new version is more accurate - only re-running BacktestEngine proves
/// or disproves that, honestly, the same way it did for v1. What changed here is give the model
/// better raw material to work with, not assume the outcome.
/// </summary>
public static class QuantScoreCalculator
{
    /// <summary>Max |raw contribution| per component at weight 1.0 - a fixed 100-point budget
    /// (25+15+15+10+15+20) that BacktestEngine's recalibration redistributes between components
    /// without changing the overall QuantScore scale. Values are hand-picked starting points (same
    /// honesty as v1) - what's actually validated is whether recalibrating them against real history
    /// helps, not the specific numbers themselves.</summary>
    public const double TrendMax = 25.0;
    public const double MomentumMax = 15.0;
    public const double MacdMax = 15.0;
    public const double VolMax = 10.0;
    public const double MeanReversionMax = 15.0;
    public const double PriceMomentumMax = 20.0;
    public const double RelativeStrengthMax = 20.0;
    public const double InsiderPurchaseMax = 15.0;
    public const double EarningsSurpriseMax = 15.0;

    /// <summary>Thresholds set from the actual empirical distribution of the technical-only score
    /// (default weights, no sentiment) across a 33-ticker/5-year sample: median ~6, p25 ~-8, p75
    /// ~21, p95 ~42. A first attempt at these thresholds was proportionally scaled from v1's
    /// Buy&gt;20/Hold&gt;-15 (out of v1's 75-point budget) onto this design's 100-point budget - that
    /// produced an *empty* Buy bucket in walk-forward testing (v1's discrete ±15/±10/±5 buckets
    /// saturate to their max far more easily than these continuous, distance-normalized signals do,
    /// so the two systems' "typical" achieved score is not comparable just because their maximums
    /// are). These values instead sit near p70/p28 of the measured distribution, giving a
    /// non-degenerate three-way split - still a judgment call on *where* in the distribution to cut,
    /// but no longer disconnected from what scores the model actually produces.</summary>
    public const double BuyThreshold = 15.0;
    public const double HoldThreshold = -10.0;

    public sealed record Weights(
        double Trend = 1.0,
        double Momentum = 1.0,
        double Macd = 1.0,
        double Vol = 1.0,
        double MeanReversion = 1.0,
        double PriceMomentum = 1.0,
        double RelativeStrength = 1.0,
        double InsiderPurchase = 1.0,
        double EarningsSurprise = 1.0)
    {
        public static readonly Weights Default = new();
    }

    public sealed record Result(
        double TrendScore,
        bool? AboveMa50,
        bool? AboveMa200,
        bool? GoldenCross,
        double MomentumScore,
        double MacdScore,
        double VolScore,
        double VolRatio,
        double MeanReversionScore,
        double PriceMomentumScore,
        double RelativeStrengthScore,
        double InsiderPurchaseScore,
        double EarningsSurpriseScore,
        double SentimentContrib,
        double QuantScore,
        Signal Signal);

    public static bool? AboveMa(double close, double? ma) => ma is { } m ? close > m : null;

    public static bool? IsGoldenCross(double? ma50, double? ma200) =>
        ma50 is { } a && ma200 is { } b ? a > b : null;

    /// <summary>Blends distance from the 200-day MA (60% weight - the long-term trend) and the
    /// 50-day MA (40% - medium-term), each normalized so a 20%/10% move away from its respective MA
    /// maxes out the signal at ±1. Missing an MA leans fully on whichever one is available; missing
    /// both returns 0 (neutral, not penalized).</summary>
    public static double TrendSignal(double close, double? ma50, double? ma200)
    {
        double? m200Component = ma200 is { } m2 and > 0 ? Math.Clamp((close - m2) / m2 / 0.20, -1, 1) : null;
        double? m50Component = ma50 is { } m5 and > 0 ? Math.Clamp((close - m5) / m5 / 0.10, -1, 1) : null;

        return (m200Component, m50Component) switch
        {
            ({ } c200, { } c50) => 0.6 * c200 + 0.4 * c50,
            ({ } c200, null) => c200,
            (null, { } c50) => c50,
            _ => 0.0
        };
    }

    /// <summary>RSI's distance from neutral (50), scaled so RSI 20 or 80 maxes the signal at ±1.
    /// Missing RSI defaults to 50 (neutral), matching v1.</summary>
    public static double MomentumSignal(double? rsi) => Math.Clamp(((rsi ?? 50.0) - 50.0) / 30.0, -1, 1);

    /// <summary>MACD-minus-signal spread, normalized by 3% of price so it's comparable across stocks
    /// at very different price levels (MACD's absolute scale tracks the underlying price, unlike RSI
    /// or a ratio). Missing MACD/signal or non-positive price returns 0 (neutral).</summary>
    public static double MacdSignal(double? macd, double? signal, double price) =>
        macd is { } m && signal is { } s && price > 0
            ? Math.Clamp((m - s) / (0.03 * price), -1, 1)
            : 0.0;

    /// <summary>Volume ratio (latest / trailing average) centered on 1.0 (normal volume) - a 2x
    /// surge maxes at +1, zero volume maxes at -1.</summary>
    public static double VolumeSignal(double volRatio) => Math.Clamp(volRatio - 1.0, -1, 1);

    /// <summary>Bollinger %B, inverted: sitting at the lower band (%B=0, "oversold") scores +1
    /// (bullish read - mean reversion upward is the classical interpretation), the upper band
    /// (%B=1, "overbought") scores -1. Missing bands or a degenerate (zero-width) band returns 0.</summary>
    public static double MeanReversionSignal(double close, double? bbUpper, double? bbLower)
    {
        if (bbUpper is not { } u || bbLower is not { } l || u <= l) return 0.0;
        var pctB = (close - l) / (u - l);
        return Math.Clamp(1 - 2 * pctB, -1, 1);
    }

    /// <summary>21-trading-day (~1 month) rate of change, normalized so a ±15% move maxes the signal
    /// and INVERTED - a big recent run-up scores negatively, a big recent drop scores positively.
    /// This was originally built as a medium-term momentum factor (assuming a recent gain predicts
    /// further gains), but walk-forward backtesting showed a consistent *negative* correlation with
    /// forward returns at every horizon tested (5/10/20/60 trading days), growing stronger at longer
    /// horizons. That's the well-documented short-term reversal effect, not momentum: genuine
    /// academic momentum factors use 3-12 month lookbacks and explicitly exclude the most recent
    /// month precisely because 1-month returns tend to reverse rather than continue. A 21-day window
    /// alone measures reversal, so the sign is flipped here to match what the data actually shows
    /// rather than what the factor was originally assumed to be (magnitude-only recalibration can't
    /// fix a sign error - see RecalibrateWeights - so this had to be corrected at the source).
    /// Missing (insufficient history) returns 0.</summary>
    public static double PriceMomentumSignal(double? roc21Pct) =>
        roc21Pct is { } r ? -Math.Clamp(r / 15.0, -1, 1) : 0.0;

    /// <summary>Excess 21-day return versus the average of the stock's same-sector peers over the
    /// same window (cross-sectional relative strength - a stock beating its peers, not just its own
    /// history), normalized so a ±10-point excess maxes the signal. Missing (no peers resolved, or
    /// insufficient history) returns 0.</summary>
    public static double RelativeStrengthSignal(double? excessRoc21Pct) =>
        excessRoc21Pct is { } r ? Math.Clamp(r / 10.0, -1, 1) : 0.0;

    /// <summary>Days a Form-4 insider Purchase filing stays "warm" - decays linearly from +1 right
    /// after the filing to 0 by this many calendar days later.</summary>
    public const double InsiderPurchaseDecayDays = 30.0;

    /// <summary>Decays linearly from +1 (a Purchase filed today) to 0 (InsiderPurchaseDecayDays or
    /// more calendar days ago); no recent purchase (or missing data) scores 0, never negative. Insider
    /// Sale transactions are deliberately not represented here - an event-study check (see
    /// backtest_feature memory, update #11) found Sales show the same longer-horizon reversal Purchases
    /// do, suggesting a company-level-turbulence confound rather than a genuine directional signal, so
    /// only the well-documented informative side (Purchases) is used.</summary>
    public static double InsiderPurchaseSignal(double? calendarDaysSincePurchase) =>
        calendarDaysSincePurchase is { } d && d >= 0 && d <= InsiderPurchaseDecayDays
            ? 1.0 - d / InsiderPurchaseDecayDays
            : 0.0;

    /// <summary>Days an EPS surprise stays "warm" - post-earnings-announcement drift (PEAD) is one
    /// of the most replicated anomalies in the academic literature: unlike PriceMomentumSignal (which
    /// captures short-term price *reversal*), PEAD says price continues to drift in the direction of
    /// an earnings surprise for weeks afterward - a different mechanism, so a longer decay window
    /// than InsiderPurchaseDecayDays is used here.</summary>
    public const double EarningsSurpriseDecayDays = 60.0;

    /// <summary>Most recent quarter's EPS surprise % (positive = beat, negative = miss), normalized
    /// so a ±10% surprise maxes the signal, decaying linearly from full strength on the report date
    /// to zero at EarningsSurpriseDecayDays - continuation (unlike PriceMomentumSignal's inversion),
    /// since PEAD is a drift-continuation effect, not reversal. Missing surprise% or a stale/future
    /// "days since" returns 0 (neutral).</summary>
    public static double EarningsSurpriseSignal(double? surprisePercent, double? calendarDaysSinceEarnings) =>
        surprisePercent is { } sp && calendarDaysSinceEarnings is { } d && d >= 0 && d <= EarningsSurpriseDecayDays
            ? Math.Clamp(sp / 10.0, -1, 1) * (1.0 - d / EarningsSurpriseDecayDays)
            : 0.0;

    public static Result Calculate(
        double latestClose,
        double? ma50,
        double? ma200,
        double? latestRsi,
        double? latestMacd,
        double? latestSignal,
        long latestVolume,
        IReadOnlyList<long> volumes,
        long avgVolumeFull,
        double? bbUpper,
        double? bbLower,
        double? roc21Pct,
        double sentimentScore,
        Weights? weights = null,
        double sentimentWeight = 1.0,
        double? excessRoc21Pct = null,
        double? daysSinceLastInsiderPurchase = null,
        double? daysSinceLastEarnings = null,
        double? lastEarningsSurprisePercent = null)
    {
        var w = weights ?? Weights.Default;

        var aboveMa50 = AboveMa(latestClose, ma50);
        var aboveMa200 = AboveMa(latestClose, ma200);
        var goldenCross = IsGoldenCross(ma50, ma200);

        var trendScore = TrendSignal(latestClose, ma50, ma200) * TrendMax * w.Trend;
        var momentumScore = MomentumSignal(latestRsi) * MomentumMax * w.Momentum;
        var macdScore = MacdSignal(latestMacd, latestSignal, latestClose) * MacdMax * w.Macd;

        var avgVol20 = volumes.Count >= 20
            ? (long)volumes.Skip(volumes.Count - 20).Average(v => (double)v)
            : avgVolumeFull;
        var volRatio = avgVol20 > 0 ? (double)latestVolume / avgVol20 : 1.0;
        var volScore = VolumeSignal(volRatio) * VolMax * w.Vol;

        var meanReversionScore = MeanReversionSignal(latestClose, bbUpper, bbLower) * MeanReversionMax * w.MeanReversion;
        var priceMomentumScore = PriceMomentumSignal(roc21Pct) * PriceMomentumMax * w.PriceMomentum;
        var relativeStrengthScore = RelativeStrengthSignal(excessRoc21Pct) * RelativeStrengthMax * w.RelativeStrength;
        var insiderPurchaseScore = InsiderPurchaseSignal(daysSinceLastInsiderPurchase) * InsiderPurchaseMax * w.InsiderPurchase;
        var earningsSurpriseScore = EarningsSurpriseSignal(lastEarningsSurprisePercent, daysSinceLastEarnings) * EarningsSurpriseMax * w.EarningsSurprise;

        var sentimentContrib = Math.Round(sentimentScore * 40 * sentimentWeight, 2);

        var quantScore = trendScore + momentumScore + macdScore + volScore
                          + meanReversionScore + priceMomentumScore + relativeStrengthScore
                          + insiderPurchaseScore + earningsSurpriseScore + sentimentContrib;
        var signal = quantScore > BuyThreshold ? Signal.Buy : quantScore > HoldThreshold ? Signal.Hold : Signal.Avoid;

        return new Result(
            trendScore, aboveMa50, aboveMa200, goldenCross,
            momentumScore, macdScore, volScore, volRatio,
            meanReversionScore, priceMomentumScore, relativeStrengthScore, insiderPurchaseScore,
            earningsSurpriseScore, sentimentContrib, quantScore, signal);
    }
}
