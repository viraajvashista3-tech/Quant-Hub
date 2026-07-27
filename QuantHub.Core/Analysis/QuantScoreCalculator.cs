using QuantHub.Core.Models;

namespace QuantHub.Core.Analysis;

/// <summary>
/// Ports the 5-component Quant Score from stock_data.py lines 182-223, verbatim thresholds
/// and tie-break behavior included (missing RSI defaults to 50.0, missing/tied MACD scores
/// -15 not 0, volume ratio uses a 20-day average distinct from the full-period display average).
/// </summary>
public static class QuantScoreCalculator
{
    public sealed record Result(
        double TrendScore,
        bool? AboveMa50,
        bool? AboveMa200,
        bool? GoldenCross,
        double MomentumScore,
        double MacdScore,
        double VolScore,
        double VolRatio,
        double SentimentContrib,
        double QuantScore,
        Signal Signal);

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
        double sentimentScore)
    {
        bool? aboveMa200 = ma200 is { } m2 ? latestClose > m2 : null;
        bool? aboveMa50 = ma50 is { } m5 ? latestClose > m5 : null;
        bool? goldenCross = ma50 is { } gm5 && ma200 is { } gm2 ? gm5 > gm2 : null;

        var trendScore =
            (aboveMa200 is true ? 15.0 : aboveMa200 is false ? -15.0 : 0.0) +
            (aboveMa50 is true ? 10.0 : aboveMa50 is false ? -10.0 : 0.0) +
            (goldenCross is true ? 5.0 : goldenCross is false ? -5.0 : 0.0);

        var rsi = latestRsi ?? 50.0;
        var momentumScore = rsi switch
        {
            >= 70 => -10.0,
            >= 60 => 20.0,
            >= 50 => 10.0,
            >= 40 => -5.0,
            >= 30 => -15.0,
            _ => -20.0
        };

        var macd = latestMacd ?? 0.0;
        var signalLine = latestSignal ?? 0.0;
        var macdScore = macd > signalLine ? 15.0 : -15.0;

        var avgVol20 = volumes.Count >= 20
            ? (long)volumes.Skip(volumes.Count - 20).Average(v => (double)v)
            : avgVolumeFull;
        var volRatio = avgVol20 > 0 ? (double)latestVolume / avgVol20 : 1.0;
        var volScore = volRatio >= 1.5 ? 10.0 : volRatio >= 1.0 ? 5.0 : 0.0;

        var sentimentContrib = Math.Round(sentimentScore * 40, 2);

        var quantScore = trendScore + momentumScore + macdScore + volScore + sentimentContrib;
        var signal = quantScore > 20 ? Signal.Buy : quantScore > -15 ? Signal.Hold : Signal.Avoid;

        return new Result(
            trendScore, aboveMa50, aboveMa200, goldenCross,
            momentumScore, macdScore, volScore, volRatio,
            sentimentContrib, quantScore, signal);
    }
}
