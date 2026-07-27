using QuantHub.Core.MarketPulse;
using QuantHub.Core.Models;

namespace QuantHub.Desktop.Tests;

public class MarketPulseServiceTests
{
    [Theory]
    [InlineData(35, "Extreme Fear")]
    [InlineData(40, "Extreme Fear")]
    [InlineData(25, "Fear")]
    [InlineData(34.9, "Fear")]
    [InlineData(18, "Neutral")]
    [InlineData(24.9, "Neutral")]
    [InlineData(12, "Greed")]
    [InlineData(17.9, "Greed")]
    [InlineData(11.9, "Extreme Greed")]
    [InlineData(0, "Extreme Greed")]
    public void ComputeMood_MatchesExactVixThresholds(double vix, string expected)
    {
        Assert.Equal(expected, MarketPulseService.ComputeMood(vix));
    }

    private static MarketPulseItem Item(string symbol, string label, double change1wPct) => new()
    {
        Symbol = symbol,
        Label = label,
        Price = 100,
        Change = 0,
        ChangePct = 0,
        Change1wPct = change1wPct,
        Change1mPct = 0
    };

    [Fact]
    public void ComputeRotationNote_BestPerformerAlwaysGetsLiteralPlusSign_EvenWhenNegative()
    {
        // All sectors down on the week - the "best" (least bad) performer is still negative,
        // but the original hardcodes a "+" before it regardless, producing "+-0.3%".
        var sectors = new List<MarketPulseItem>
        {
            Item("XLK", "Technology", -0.3),
            Item("XLE", "Energy", -2.1)
        };

        var note = MarketPulseService.ComputeRotationNote(sectors);

        Assert.Equal("Money is rotating into Technology (+-0.3% 1W) and out of Energy (-2.1% 1W).", note);
    }

    [Fact]
    public void ComputeRotationNote_NormalCase_BestPositiveWorstNegative()
    {
        var sectors = new List<MarketPulseItem>
        {
            Item("XLK", "Technology", 3.2),
            Item("XLE", "Energy", -1.8)
        };

        var note = MarketPulseService.ComputeRotationNote(sectors);

        Assert.Equal("Money is rotating into Technology (+3.2% 1W) and out of Energy (-1.8% 1W).", note);
    }

    [Fact]
    public void ComputeRotationNote_EmptySectors_ReturnsEmptyString()
    {
        Assert.Equal("", MarketPulseService.ComputeRotationNote([]));
    }
}
