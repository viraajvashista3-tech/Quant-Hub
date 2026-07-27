using QuantHub.Core.Formatting;

namespace QuantHub.Desktop.Tests;

public class FormatTests
{
    [Fact]
    public void LargeNumber_Null_ReturnsDash() => Assert.Equal("-", Format.LargeNumber(null));

    [Theory]
    [InlineData(1_234_567_890_123.0, "1.23T")]
    [InlineData(1_234_567_890.0, "1.23B")]
    [InlineData(1_234_567.0, "1.23M")]
    public void LargeNumber_UsesSuffixesAboveThresholds(double value, string expected)
    {
        Assert.Equal(expected, Format.LargeNumber(value));
    }

    [Fact]
    public void LargeNumber_WholeNumberBelowMillion_HasNoDecimalPadding()
    {
        // toLocaleString(undefined, { maximumFractionDigits: 2 }) does not pad whole numbers -
        // this must NOT come out as "1,234.00".
        Assert.Equal("1,234", Format.LargeNumber(1234));
    }

    [Fact]
    public void LargeNumber_FractionalBelowMillion_ShowsUpToTwoDecimalsNoPadding()
    {
        Assert.Equal("1,234.5", Format.LargeNumber(1234.5));
        Assert.Equal("1,234.57", Format.LargeNumber(1234.567));
    }

    [Fact]
    public void Percent_Null_ReturnsDash() => Assert.Equal("-", Format.Percent(null));

    [Fact]
    public void Percent_PositivePrependsPlusSign() => Assert.Equal("+5.00%", Format.Percent(5));

    [Fact]
    public void Percent_ZeroHasNoSign() => Assert.Equal("0.00%", Format.Percent(0));

    [Fact]
    public void Percent_NegativeHasNoExplicitPlusButKeepsMinus() => Assert.Equal("-5.00%", Format.Percent(-5));

    [Fact]
    public void Currency_FormatsWithDollarSignAndTwoDecimals() => Assert.Equal("$12.30", Format.Currency(12.3));

    [Fact]
    public void Value_PercentBranch_NeverPrependsPlus_UnlikeThePercentHelper()
    {
        // formatValue's "percent" case intentionally differs from Percent(): no "+" prefix ever.
        Assert.Equal("5.00%", Format.Value(5, Format.ValueType.Percent));
        Assert.Equal("-5.00%", Format.Value(-5, Format.ValueType.Percent));
    }

    [Fact]
    public void Value_NumberBranch_DelegatesToLargeNumber()
    {
        Assert.Equal("1.23M", Format.Value(1_234_567, Format.ValueType.Number));
    }

    [Fact]
    public void Value_CurrencyBranch_DelegatesToCurrency()
    {
        Assert.Equal("$12.30", Format.Value(12.3, Format.ValueType.Currency));
    }
}
