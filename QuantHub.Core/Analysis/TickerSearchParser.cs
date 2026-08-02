using System.Text.Json;
using QuantHub.Core.Models;

namespace QuantHub.Core.Analysis;

/// <summary>Parses Yahoo's v1/finance/search response (a flat "quotes" array, unlike quoteSummary's
/// module-keyed {raw,fmt} shape - so this doesn't go through YahooJson's helpers). Pulled out as its
/// own testable parser rather than inlined in YahooFinanceClient.SearchAsync since the quoteType
/// filtering below is real logic worth locking down, unlike GetChartAsync's parsing (which has none
/// and stays inline).</summary>
public static class TickerSearchParser
{
    private static readonly HashSet<string> AllowedQuoteTypes = new(StringComparer.OrdinalIgnoreCase) { "EQUITY", "ETF" };

    public static IReadOnlyList<TickerSearchResult> Parse(JsonElement root)
    {
        if (!root.TryGetProperty("quotes", out var quotes) || quotes.ValueKind != JsonValueKind.Array)
            return [];

        var results = new List<TickerSearchResult>();
        foreach (var q in quotes.EnumerateArray())
        {
            var quoteType = Str(q, "quoteType");
            if (quoteType is null || !AllowedQuoteTypes.Contains(quoteType)) continue;

            var symbol = Str(q, "symbol");
            if (string.IsNullOrWhiteSpace(symbol)) continue;

            var name = Str(q, "shortname") ?? Str(q, "longname") ?? symbol;
            results.Add(new TickerSearchResult(symbol, name, Str(q, "exchDisp")));
        }
        return results;
    }

    private static string? Str(JsonElement el, string field) =>
        el.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
