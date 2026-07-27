using QuantHub.Core.Analysis;

namespace QuantHub.Desktop.Tests;

public class IndicatorsTests
{
    [Fact]
    public void Sma_IsNullUntilWindowFills()
    {
        double[] closes = [1, 2, 3, 4, 5];
        var sma = Indicators.Sma(closes, 3);

        Assert.Null(sma[0]);
        Assert.Null(sma[1]);
        Assert.Equal(2.0, sma[2]);
        Assert.Equal(3.0, sma[3]);
        Assert.Equal(4.0, sma[4]);
    }

    [Fact]
    public void EwmAlpha_RecursesFromFirstNonNullValue()
    {
        double?[] values = [1.0, 2.0, 3.0];
        var result = Indicators.EwmAlpha(values, 0.5);

        Assert.Equal(1.0, result[0]);
        Assert.Equal(1.5, result[1]);
        Assert.Equal(2.25, result[2]);
    }

    [Fact]
    public void Rsi_FirstValueIsNullAndApproaches100OnPureUptrend()
    {
        double[] closes = [44.0, 44.25, 44.5, 44.75, 45.0];
        var rsi = Indicators.Rsi(closes);

        Assert.Null(rsi[0]);
        Assert.True(rsi[^1] > 99.9);
    }

    [Fact]
    public void Rsi_ApproachesZeroOnPureDowntrend()
    {
        double[] closes = [45.0, 44.75, 44.5, 44.25, 44.0];
        var rsi = Indicators.Rsi(closes);

        Assert.True(rsi[^1] < 0.1);
    }

    [Fact]
    public void BollingerBands_FlatSeriesHasZeroWidthBand()
    {
        var closes = Enumerable.Repeat(100.0, 20).ToArray();
        var (upper, lower, ma20) = Indicators.BollingerBands(closes);

        Assert.Null(ma20[18]);
        Assert.Equal(100.0, ma20[19]);
        Assert.Equal(100.0, upper[19]);
        Assert.Equal(100.0, lower[19]);
    }

    [Fact]
    public void FlatPriceSeries_ProducesZeroVolatilitySharpeAndDrawdown()
    {
        var closes = Enumerable.Repeat(100.0, 10).ToArray();

        Assert.Equal(0.0, Indicators.AnnualizedVolatility(closes));
        Assert.Equal(0.0, Indicators.SharpeRatio(closes));
        Assert.Equal(0.0, Indicators.MaxDrawdownPercent(closes));
    }

    [Fact]
    public void AnnualizedVolatility_NullWhenFewerThanTwoReturns()
    {
        double[] closes = [100.0];
        Assert.Null(Indicators.AnnualizedVolatility(closes));
        Assert.Null(Indicators.SharpeRatio(closes));
    }

    [Fact]
    public void MaxDrawdown_CapturesADip()
    {
        double[] closes = [100, 110, 90, 100];
        var dd = Indicators.MaxDrawdownPercent(closes);

        Assert.NotNull(dd);
        Assert.True(dd < 0);
    }

    [Fact]
    public void Macd_SignalIsEmaOfMacdSeries()
    {
        var closes = Enumerable.Range(0, 40).Select(i => 100.0 + i).ToArray();
        var (macd, signal) = Indicators.Macd(closes);

        Assert.NotNull(macd[0]);
        Assert.NotNull(signal[0]);
        // A steady uptrend puts the fast EMA above the slow EMA, so MACD should be positive.
        Assert.True(macd[^1] > 0);
    }
}
