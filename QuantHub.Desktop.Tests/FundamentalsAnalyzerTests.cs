using System.Text.Json;
using QuantHub.Core.Analysis;

namespace QuantHub.Desktop.Tests;

public class FundamentalsAnalyzerTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void GrahamNumber_ComputedWhenBothEpsAndBvpsPositive()
    {
        var result = Parse("""
        {
            "price": { "shortName": "Test Co" },
            "defaultKeyStatistics": { "trailingEps": { "raw": 5.0 }, "bookValue": { "raw": 20.0 } }
        }
        """);

        var f = FundamentalsAnalyzer.Build("test", result);

        // sqrt(22.5 * 5.0 * 20.0) = sqrt(2250) = 47.4341...
        Assert.NotNull(f.GrahamNumber);
        Assert.Equal(Math.Round(Math.Sqrt(22.5 * 5.0 * 20.0), 2), f.GrahamNumber!.Value, 2);
    }

    [Fact]
    public void GrahamNumber_NullWhenEpsIsNegative()
    {
        var result = Parse("""
        {
            "defaultKeyStatistics": { "trailingEps": { "raw": -1.0 }, "bookValue": { "raw": 20.0 } }
        }
        """);

        var f = FundamentalsAnalyzer.Build("test", result);
        Assert.Null(f.GrahamNumber);
    }

    [Fact]
    public void GrahamNumber_NullWhenBvpsMissing()
    {
        var result = Parse("""
        {
            "defaultKeyStatistics": { "trailingEps": { "raw": 5.0 } }
        }
        """);

        var f = FundamentalsAnalyzer.Build("test", result);
        Assert.Null(f.GrahamNumber);
    }

    [Fact]
    public void Build_ReadsAcrossModulesAndFallsBackToTickerForMissingName()
    {
        var result = Parse("""
        {
            "summaryDetail": { "marketCap": { "raw": 1000000 }, "trailingPE": 15.5 },
            "assetProfile": { "sector": "Technology", "industry": "Software" }
        }
        """);

        var f = FundamentalsAnalyzer.Build("acme", result);

        Assert.Equal("ACME", f.Ticker);
        Assert.Equal("ACME", f.Name);
        Assert.Equal(1000000, f.MarketCap);
        Assert.Equal(15.5, f.Pe);
        Assert.Equal("Technology", f.Sector);
        Assert.Equal("Software", f.Industry);
    }

    [Fact]
    public void Build_ReadsDividendYieldRateAndPayoutRatioFromSummaryDetail()
    {
        var result = Parse("""
        {
            "summaryDetail": {
                "dividendYield": { "raw": 0.025 },
                "dividendRate": { "raw": 1.92 },
                "payoutRatio": { "raw": 0.35 }
            }
        }
        """);

        var f = FundamentalsAnalyzer.Build("test", result);

        Assert.Equal(0.025, f.DividendYield);
        Assert.Equal(1.92, f.DividendRate);
        Assert.Equal(0.35, f.PayoutRatio);
    }

    [Fact]
    public void Build_MissingDividendFields_AllNull()
    {
        var result = Parse("{ \"summaryDetail\": {} }");
        var f = FundamentalsAnalyzer.Build("test", result);

        Assert.Null(f.DividendYield);
        Assert.Null(f.DividendRate);
        Assert.Null(f.PayoutRatio);
    }
}
