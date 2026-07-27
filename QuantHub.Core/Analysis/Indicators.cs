namespace QuantHub.Core.Analysis;

/// <summary>
/// Ports the technical-indicator math from stock_data.py's calculate_indicators/overview
/// functions, matching pandas semantics exactly (rolling min_periods=window, ewm(adjust=False)
/// recursion, ddof=1 sample stdev) rather than textbook formulas that would produce different
/// numbers on the same input.
/// </summary>
public static class Indicators
{
    public static double?[] Sma(IReadOnlyList<double> values, int window)
    {
        var result = new double?[values.Count];
        double sum = 0;
        for (var i = 0; i < values.Count; i++)
        {
            sum += values[i];
            if (i >= window) sum -= values[i - window];
            result[i] = i >= window - 1 ? sum / window : null;
        }
        return result;
    }

    public static double?[] SampleStd(IReadOnlyList<double> values, int window)
    {
        var result = new double?[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            if (i < window - 1) { result[i] = null; continue; }
            double mean = 0;
            for (var j = i - window + 1; j <= i; j++) mean += values[j];
            mean /= window;
            double sumSq = 0;
            for (var j = i - window + 1; j <= i; j++) sumSq += (values[j] - mean) * (values[j] - mean);
            result[i] = Math.Sqrt(sumSq / (window - 1));
        }
        return result;
    }

    /// <summary>EWM recursion with adjust=False: seeds on the first non-null value, holds through leading nulls.</summary>
    public static double?[] EwmAlpha(IReadOnlyList<double?> values, double alpha)
    {
        var result = new double?[values.Count];
        double? ema = null;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } v)
            {
                result[i] = ema;
                continue;
            }
            ema = ema is null ? v : alpha * v + (1 - alpha) * ema.Value;
            result[i] = ema;
        }
        return result;
    }

    public static double?[] EwmSpan(IReadOnlyList<double> values, int span)
    {
        var alpha = 2.0 / (span + 1);
        return EwmAlpha(values.Select(v => (double?)v).ToArray(), alpha);
    }

    public static (double?[] Macd, double?[] Signal) Macd(IReadOnlyList<double> closes)
    {
        var ema12 = EwmSpan(closes, 12);
        var ema26 = EwmSpan(closes, 26);
        var macd = new double?[closes.Count];
        for (var i = 0; i < closes.Count; i++)
        {
            macd[i] = ema12[i] is { } a && ema26[i] is { } b ? a - b : null;
        }
        var signal = EwmAlpha(macd, 2.0 / (9 + 1));
        return (macd, signal);
    }

    public static double?[] Rsi(IReadOnlyList<double> closes)
    {
        var n = closes.Count;
        var gain = new double?[n];
        var loss = new double?[n];
        for (var i = 1; i < n; i++)
        {
            var delta = closes[i] - closes[i - 1];
            gain[i] = Math.Max(delta, 0);
            loss[i] = Math.Max(-delta, 0);
        }

        var gainEwm = EwmAlpha(gain, 1.0 / 14);
        var lossEwm = EwmAlpha(loss, 1.0 / 14);
        var rsi = new double?[n];
        for (var i = 0; i < n; i++)
        {
            if (gainEwm[i] is { } g && lossEwm[i] is { } l)
            {
                rsi[i] = 100 - 100 / (1 + g / (l + 1e-10));
            }
        }
        return rsi;
    }

    public static (double?[] Upper, double?[] Lower, double?[] Ma20) BollingerBands(IReadOnlyList<double> closes)
    {
        var ma20 = Sma(closes, 20);
        var std20 = SampleStd(closes, 20);
        var upper = new double?[closes.Count];
        var lower = new double?[closes.Count];
        for (var i = 0; i < closes.Count; i++)
        {
            if (ma20[i] is { } m && std20[i] is { } s)
            {
                upper[i] = m + 2 * s;
                lower[i] = m - 2 * s;
            }
        }
        return (upper, lower, ma20);
    }

    public static double[] DailyReturns(IReadOnlyList<double> closes)
    {
        if (closes.Count < 2) return [];
        var result = new double[closes.Count - 1];
        for (var i = 1; i < closes.Count; i++)
        {
            result[i - 1] = (closes[i] - closes[i - 1]) / closes[i - 1];
        }
        return result;
    }

    private static double Mean(IReadOnlyList<double> values) => values.Count == 0 ? 0 : values.Sum() / values.Count;

    private static double SampleStdDev(IReadOnlyList<double> values)
    {
        var mean = Mean(values);
        var sumSq = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSq / (values.Count - 1));
    }

    /// <summary>Null when fewer than 2 daily returns exist (pandas sample-stdev of &lt;2 points is NaN).</summary>
    public static double? AnnualizedVolatility(IReadOnlyList<double> closes)
    {
        var rets = DailyReturns(closes);
        if (rets.Length < 2) return null;
        return SampleStdDev(rets) * Math.Sqrt(252) * 100;
    }

    /// <summary>Hardcoded 4.5% annual risk-free rate, matching the Python original exactly.</summary>
    public static double? SharpeRatio(IReadOnlyList<double> closes, double annualRiskFreeRate = 0.045)
    {
        var rets = DailyReturns(closes);
        if (rets.Length < 2) return null;
        var rfDaily = annualRiskFreeRate / 252;
        var excess = rets.Select(r => r - rfDaily).ToArray();
        var std = SampleStdDev(excess);
        if (std == 0) return 0.0;
        return Mean(excess) / std * Math.Sqrt(252);
    }

    public static double? MaxDrawdownPercent(IReadOnlyList<double> closes)
    {
        var rets = DailyReturns(closes);
        if (rets.Length == 0) return null;
        var cumulative = 1.0;
        var rollingMax = double.MinValue;
        var maxDrawdown = 0.0;
        foreach (var r in rets)
        {
            cumulative *= 1 + r;
            rollingMax = Math.Max(rollingMax, cumulative);
            var drawdown = (cumulative - rollingMax) / rollingMax;
            maxDrawdown = Math.Min(maxDrawdown, drawdown);
        }
        return maxDrawdown * 100;
    }
}
