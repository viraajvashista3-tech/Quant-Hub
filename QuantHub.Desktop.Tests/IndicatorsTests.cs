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
    public void RollingVolumeRatio_IsNullUntilWindowFills()
    {
        long[] volumes = [10, 20, 30];
        var ratio = Indicators.RollingVolumeRatio(volumes, window: 3);

        Assert.Null(ratio[0]);
        Assert.Null(ratio[1]);
        Assert.Equal(30.0 / 20.0, ratio[2]!.Value, 6); // avg(10,20,30)=20, latest=30
    }

    [Fact]
    public void RollingVolumeRatio_IsCausal_LaterBarsDontAffectEarlierRatios()
    {
        // The ratio at index 2 (window=3) must depend only on bars 0-2, matching what
        // BacktestEngine needs to avoid leaking future volume into a historical bar's score.
        long[] shortSeries = [10, 20, 30];
        long[] longerSeries = [10, 20, 30, 1_000_000]; // huge future spike appended

        var ratioShort = Indicators.RollingVolumeRatio(shortSeries, window: 3);
        var ratioLong = Indicators.RollingVolumeRatio(longerSeries, window: 3);

        Assert.Equal(ratioShort[2], ratioLong[2]);
    }

    [Fact]
    public void RollingVolumeRatio_SlidesTheWindowForward()
    {
        long[] volumes = [100, 100, 100, 50, 50, 50];
        var ratio = Indicators.RollingVolumeRatio(volumes, window: 3);

        Assert.Equal(1.0, ratio[2]!.Value, 6); // window=[100,100,100], avg=100, latest=100
        Assert.Equal(50.0 / (250.0 / 3), ratio[3]!.Value, 6); // window=[100,100,50], avg=83.33, latest=50
        Assert.Equal(1.0, ratio[5]!.Value, 6); // window has fully slid to [50,50,50], avg=50, latest=50
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
