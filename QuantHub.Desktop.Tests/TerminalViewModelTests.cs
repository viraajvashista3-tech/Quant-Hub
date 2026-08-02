using QuantHub.Core.Models;
using QuantHub.Desktop.ViewModels.Pages;

namespace QuantHub.Desktop.Tests;

public class TerminalViewModelTests
{
    private static StockOverview Overview(Signal signal, double score, double price = 100) => new()
    {
        Ticker = "TEST",
        Name = "Test Co",
        Price = price,
        QuantScore = score,
        Signal = signal
    };

    private static AnalystData Analyst(string consensus, double? targetMean, double? currentPrice) => new()
    {
        Ticker = "TEST",
        ConsensusRating = consensus,
        TargetMean = targetMean,
        CurrentPrice = currentPrice
    };

    [Fact]
    public void BuildRecommendationLine_BuySignalWithUpside_NamesActualNumbers()
    {
        var line = TerminalViewModel.BuildRecommendationLine(Overview(Signal.Buy, 42.3, 100), Analyst("Buy", 120, 100));

        Assert.Contains("TEST is a Buy (Quant Score 42)", line);
        Assert.Contains("Buy", line);
        Assert.Contains("20.0% upside", line);
        Assert.Contains("$120.00", line);
    }

    [Fact]
    public void BuildRecommendationLine_AvoidSignalWithDownside_SaysDownside()
    {
        var line = TerminalViewModel.BuildRecommendationLine(Overview(Signal.Avoid, -12, 100), Analyst("Sell", 80, 100));

        Assert.Contains("TEST is a Avoid", line);
        Assert.Contains("20.0% downside", line);
    }

    [Fact]
    public void BuildRecommendationLine_HoldSignal_UsesHoldWord()
    {
        var line = TerminalViewModel.BuildRecommendationLine(Overview(Signal.Hold, 3), null);
        Assert.Contains("TEST is a Hold", line);
    }

    [Fact]
    public void BuildRecommendationLine_NullAnalyst_DegradesToNoCoverageSentence()
    {
        var line = TerminalViewModel.BuildRecommendationLine(Overview(Signal.Buy, 42), null);
        Assert.Contains("Analyst coverage isn't available", line);
    }

    [Fact]
    public void BuildRecommendationLine_NARating_DegradesToNoCoverageSentence()
    {
        var line = TerminalViewModel.BuildRecommendationLine(Overview(Signal.Buy, 42), Analyst("N/A", 120, 100));
        Assert.Contains("Analyst coverage isn't available", line);
    }

    [Fact]
    public void BuildRecommendationLine_NoPriceTarget_DegradesToRatingOnlySentence()
    {
        var line = TerminalViewModel.BuildRecommendationLine(Overview(Signal.Buy, 42), Analyst("Buy", null, 100));
        Assert.Contains("Wall Street consensus is Buy, but no price target is available.", line);
    }

    [Fact]
    public void BuildRecommendationLine_AnalystCurrentPriceMissing_FallsBackToOverviewPrice()
    {
        var line = TerminalViewModel.BuildRecommendationLine(Overview(Signal.Buy, 42, 100), Analyst("Buy", 110, null));
        Assert.Contains("10.0% upside", line);
    }
}
