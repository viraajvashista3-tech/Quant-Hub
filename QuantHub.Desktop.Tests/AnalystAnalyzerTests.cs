using System.Text.Json;
using QuantHub.Core.Analysis;

namespace QuantHub.Desktop.Tests;

public class AnalystAnalyzerTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Theory]
    [InlineData("strong buy", "Strong Buy")]
    [InlineData("STRONG BUY", "Strong Buy")]
    [InlineData("underperform", "Underperform")]
    public void TitleCase_MatchesPythonStrTitle(string input, string expected)
    {
        Assert.Equal(expected, AnalystAnalyzer.TitleCase(input));
    }

    [Fact]
    public void Build_TitleCasesConsensusAndReadsTargets()
    {
        var result = Parse("""
        {
            "financialData": {
                "recommendationKey": "strong_buy",
                "numberOfAnalystOpinions": { "raw": 12 },
                "currentPrice": { "raw": 150.5 },
                "targetLowPrice": { "raw": 100 },
                "targetMeanPrice": { "raw": 160 },
                "targetHighPrice": { "raw": 200 }
            }
        }
        """);

        var analyst = AnalystAnalyzer.Build("test", result);

        Assert.Equal("Strong Buy", analyst.ConsensusRating);
        Assert.Equal(12, analyst.NumAnalysts);
        Assert.Equal(150.5, analyst.CurrentPrice);
        Assert.Equal(100, analyst.TargetLow);
        Assert.Equal(160, analyst.TargetMean);
        Assert.Equal(200, analyst.TargetHigh);
    }

    [Fact]
    public void Build_MissingRecommendationKey_DefaultsToNA()
    {
        var result = Parse("{ \"financialData\": {} }");
        var analyst = AnalystAnalyzer.Build("test", result);
        Assert.Equal("N/A", analyst.ConsensusRating);
    }

    [Fact]
    public void Build_RecommendationTrend_ZeroMonthPeriodLabeledCurrent()
    {
        // Yahoo's current-month period is "0m" (no leading "-"); previous months are "-1m"/"-2m"/etc.
        var result = Parse("""
        {
            "recommendationTrend": {
                "trend": [
                    { "period": "0m", "strongBuy": 5, "buy": 10, "hold": 3, "sell": 0, "strongSell": 0 },
                    { "period": "-1m", "strongBuy": 4, "buy": 9, "hold": 4, "sell": 1, "strongSell": 0 },
                    { "period": "-2m", "strongBuy": 3, "buy": 8, "hold": 5, "sell": 1, "strongSell": 0 }
                ]
            }
        }
        """);

        var analyst = AnalystAnalyzer.Build("test", result);

        Assert.Equal("Current", analyst.RecommendationTrend![0].Period);
        Assert.Equal("1mo ago", analyst.RecommendationTrend[1].Period);
        Assert.Equal("2mo ago", analyst.RecommendationTrend[2].Period);
    }

    [Fact]
    public void Build_RecommendationTrend_UnparseablePeriod_KeepsRawString()
    {
        var result = Parse("""
        {
            "recommendationTrend": {
                "trend": [ { "period": "unexpected", "strongBuy": 0, "buy": 0, "hold": 0, "sell": 0, "strongSell": 0 } ]
            }
        }
        """);

        var analyst = AnalystAnalyzer.Build("test", result);

        Assert.Equal("unexpected", analyst.RecommendationTrend![0].Period);
    }

    [Fact]
    public void Build_UpgradeDowngradeHistory_ParsesFirmAndDate()
    {
        var result = Parse("""
        {
            "upgradeDowngradeHistory": {
                "history": [
                    { "epochGradeDate": 1700000000, "firm": "Morgan Stanley", "toGrade": "Overweight", "fromGrade": "Equal-Weight", "action": "up" }
                ]
            }
        }
        """);

        var analyst = AnalystAnalyzer.Build("test", result);

        var action = Assert.Single(analyst.RecentActions!);
        Assert.Equal("Morgan Stanley", action.Firm);
        Assert.Equal("Overweight", action.ToGrade);
        Assert.Equal("up", action.Action);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime.ToString("yyyy-MM-dd"), action.Date);
    }

    [Theory]
    [InlineData(160, 150.5, 6.31)]
    [InlineData(100, 150.5, -33.55)]
    public void UpsidePotentialPct_ComputesPercentFromCurrentPrice(double target, double price, double expected)
    {
        Assert.Equal(expected, AnalystAnalyzer.UpsidePotentialPct(target, price)!.Value, 2);
    }

    [Fact]
    public void UpsidePotentialPct_NullTarget_ReturnsNull()
    {
        Assert.Null(AnalystAnalyzer.UpsidePotentialPct(null, 150.5));
    }

    [Fact]
    public void UpsidePotentialPct_NullPrice_ReturnsNull()
    {
        Assert.Null(AnalystAnalyzer.UpsidePotentialPct(160, null));
    }

    [Fact]
    public void UpsidePotentialPct_ZeroOrNegativePrice_ReturnsNull()
    {
        Assert.Null(AnalystAnalyzer.UpsidePotentialPct(160, 0));
        Assert.Null(AnalystAnalyzer.UpsidePotentialPct(160, -10));
    }

    [Theory]
    [InlineData("Strong Buy", 0)]
    [InlineData("Buy", 1)]
    [InlineData("Hold", 2)]
    [InlineData("Sell", 3)]
    [InlineData("Strong Sell", 4)]
    [InlineData("N/A", 5)]
    [InlineData(null, 5)]
    [InlineData("garbage", 5)]
    public void ConsensusRatingRank_OrdersBestToBuyFirst(string? rating, int expected)
    {
        Assert.Equal(expected, AnalystAnalyzer.ConsensusRatingRank(rating));
    }
}
