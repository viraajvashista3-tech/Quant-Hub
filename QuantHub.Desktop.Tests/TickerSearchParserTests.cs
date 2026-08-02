using System.Text.Json;
using QuantHub.Core.Analysis;

namespace QuantHub.Desktop.Tests;

public class TickerSearchParserTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Parse_KeepsEquityAndEtfQuoteTypes()
    {
        var root = Parse("""
        {
            "quotes": [
                { "symbol": "AAPL", "shortname": "Apple Inc.", "exchDisp": "NASDAQ", "quoteType": "EQUITY" },
                { "symbol": "SPY", "shortname": "SPDR S&P 500 ETF Trust", "exchDisp": "PCX", "quoteType": "ETF" }
            ]
        }
        """);

        var results = TickerSearchParser.Parse(root);

        Assert.Equal(2, results.Count);
        Assert.Equal("AAPL", results[0].Symbol);
        Assert.Equal("Apple Inc.", results[0].Name);
        Assert.Equal("NASDAQ", results[0].Exchange);
        Assert.Equal("SPY", results[1].Symbol);
    }

    [Theory]
    [InlineData("OPTION")]
    [InlineData("CRYPTOCURRENCY")]
    [InlineData("FUTURE")]
    [InlineData("INDEX")]
    public void Parse_FiltersOutNonEquityNonEtfQuoteTypes(string quoteType)
    {
        var root = Parse($$"""
        {
            "quotes": [ { "symbol": "XYZ", "shortname": "Something", "quoteType": "{{quoteType}}" } ]
        }
        """);

        Assert.Empty(TickerSearchParser.Parse(root));
    }

    [Fact]
    public void Parse_FallsBackFromShortnameToLongnameToSymbol()
    {
        var root = Parse("""
        {
            "quotes": [
                { "symbol": "A", "quoteType": "EQUITY" },
                { "symbol": "B", "longname": "B Long Name", "quoteType": "EQUITY" },
                { "symbol": "C", "shortname": "C Short Name", "longname": "C Long Name", "quoteType": "EQUITY" }
            ]
        }
        """);

        var results = TickerSearchParser.Parse(root);

        Assert.Equal("A", results[0].Name); // no shortname/longname - falls back to the symbol itself
        Assert.Equal("B Long Name", results[1].Name);
        Assert.Equal("C Short Name", results[2].Name); // shortname wins over longname when both present
    }

    [Fact]
    public void Parse_MissingQuotesArray_ReturnsEmpty()
    {
        Assert.Empty(TickerSearchParser.Parse(Parse("{}")));
    }

    [Fact]
    public void Parse_SkipsEntriesWithBlankOrMissingSymbol()
    {
        var root = Parse("""
        {
            "quotes": [
                { "shortname": "No Symbol", "quoteType": "EQUITY" },
                { "symbol": "", "shortname": "Blank Symbol", "quoteType": "EQUITY" },
                { "symbol": "OK", "shortname": "Fine", "quoteType": "EQUITY" }
            ]
        }
        """);

        var results = TickerSearchParser.Parse(root);

        Assert.Single(results);
        Assert.Equal("OK", results[0].Symbol);
    }
}
