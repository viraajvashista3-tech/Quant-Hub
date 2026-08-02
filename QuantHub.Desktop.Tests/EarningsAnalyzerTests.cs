using System.Text.Json;
using QuantHub.Core.Analysis;

namespace QuantHub.Desktop.Tests;

public class EarningsAnalyzerTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Build_SortsHistoryNewestFirstRegardlessOfInputOrder()
    {
        var result = Parse("""
        {
            "earningsHistory": {
                "history": [
                    { "quarter": { "raw": 1704067200 }, "epsActual": { "raw": 1.5 }, "epsEstimate": { "raw": 1.4 }, "surprisePercent": { "raw": 7.1 } },
                    { "quarter": { "raw": 1711929600 }, "epsActual": { "raw": 1.2 }, "epsEstimate": { "raw": 1.3 }, "surprisePercent": { "raw": -7.7 } }
                ]
            }
        }
        """);

        var e = EarningsAnalyzer.Build("test", result);

        Assert.Equal(2, e.History.Count);
        Assert.Equal("2024-04-01", e.History[0].Date);
        Assert.Equal("2024-01-01", e.History[1].Date);
        Assert.Equal(1.2, e.History[0].EpsActual);
        Assert.Equal(-7.7, e.History[0].SurprisePercent);
    }

    [Fact]
    public void Build_ParsesNextEarningsDateAndExDividendDateFromCalendarEvents()
    {
        var result = Parse("""
        {
            "calendarEvents": {
                "earnings": { "earningsDate": [ { "raw": 1735689600 } ] },
                "exDividendDate": { "raw": 1730419200 }
            }
        }
        """);

        var e = EarningsAnalyzer.Build("test", result);

        Assert.Equal("2025-01-01", e.NextEarningsDate);
        Assert.Equal("2024-11-01", e.ExDividendDate);
    }

    [Fact]
    public void Build_ReturnsEmptyHistoryAndNullDatesWhenModulesMissing()
    {
        var result = Parse("{}");

        var e = EarningsAnalyzer.Build("test", result);

        Assert.Empty(e.History);
        Assert.Null(e.NextEarningsDate);
        Assert.Null(e.ExDividendDate);
    }
}
