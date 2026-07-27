using System.Text.Json;
using QuantHub.Core.Models;

namespace QuantHub.Core.Analysis;

/// <summary>Ports the insider command (stock_data.py lines 591-676): the exact substring-precedence
/// transaction classification, the 6-month purchase/sale summary, and net-sentiment counting.
///
/// The 6-month summary is built directly from Yahoo's netSharePurchaseActivity raw fields
/// (buyInfoShares/buyInfoCount -&gt; Purchases, sellInfoShares/sellInfoCount -&gt; Sales) rather than
/// replicating yfinance's internal insider_purchases DataFrame row layout, which isn't documented
/// in stock_data.py itself and can't be verified without the yfinance source.</summary>
public static class InsiderAnalyzer
{
    public static readonly string[] Modules =
        ["price", "assetProfile", "defaultKeyStatistics", "insiderTransactions", "netSharePurchaseActivity"];

    public static InsiderData Build(string ticker, JsonElement result, string? name)
    {
        var upper = ticker.ToUpperInvariant();
        string[] ownershipModules = ["defaultKeyStatistics", "majorHoldersBreakdown", "financialData"];
        var insiderPct = Yahoo.YahooJson.RawAny(result, ownershipModules, "heldPercentInsiders");
        var institutionPct = Yahoo.YahooJson.RawAny(result, ownershipModules, "heldPercentInstitutions");

        var transactions = new List<InsiderTransaction>();
        if (result.TryGetProperty("insiderTransactions", out var itMod) &&
            itMod.TryGetProperty("transactions", out var txArr) && txArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in txArr.EnumerateArray().Take(50))
            {
                var text = GetRawString(row, "transactionText") ?? "";
                var transactionType = ClassifyTransaction(text);

                string? date = null;
                if (row.TryGetProperty("startDate", out var sd) && ExtractRawLong(sd) is { } epoch)
                {
                    date = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime.ToString("yyyy-MM-dd");
                }

                transactions.Add(new InsiderTransaction
                {
                    Insider = GetRawString(row, "filerName") ?? "",
                    Position = NullIfEmpty(GetRawString(row, "filerRelation")),
                    TransactionType = transactionType,
                    Shares = row.TryGetProperty("shares", out var sh) ? ExtractRawLong(sh) : null,
                    Value = row.TryGetProperty("value", out var val) ? ExtractRawDouble(val) : null,
                    Text = text,
                    Date = date,
                    Ownership = GetRawString(row, "ownership") ?? "D"
                });
            }
        }

        InsiderPurchases6m? purchases6m = null;
        if (result.TryGetProperty("netSharePurchaseActivity", out var npa))
        {
            purchases6m = new InsiderPurchases6m
            {
                PurchaseShares = npa.TryGetProperty("buyInfoShares", out var bs) ? ExtractRawLong(bs) : null,
                PurchaseTrans = npa.TryGetProperty("buyInfoCount", out var bc) ? ExtractRawLong(bc) : null,
                SaleShares = npa.TryGetProperty("sellInfoShares", out var ss) ? ExtractRawLong(ss) : null,
                SaleTrans = npa.TryGetProperty("sellInfoCount", out var sc) ? ExtractRawLong(sc) : null
            };
        }

        var buys = transactions.Count(t => t.TransactionType == "Purchase");
        var sells = transactions.Count(t => t.TransactionType == "Sale");
        var netSentiment = buys > sells ? "Net Buyers" : sells > buys ? "Net Sellers" : "Neutral";

        return new InsiderData
        {
            Ticker = upper,
            Name = name ?? upper,
            InsiderOwnership = insiderPct,
            InstitutionalOwnership = institutionPct,
            NetSentiment = netSentiment,
            BuyCount = buys,
            SellCount = sells,
            Purchases6m = purchases6m,
            Transactions = transactions
        };
    }

    /// <summary>Exact substring-precedence chain from stock_data.py lines 601-613 - first match wins.</summary>
    internal static string ClassifyTransaction(string text)
    {
        var t = text.ToLowerInvariant();
        if (t.Contains("sale") || t.Contains("sell")) return "Sale";
        if (t.Contains("purchase") || t.Contains("buy") || t.Contains("bought")) return "Purchase";
        if (t.Contains("gift") || t.Contains("donated")) return "Gift";
        if (t.Contains("option") || t.Contains("exercise")) return "Option Exercise";
        if (t.Contains("award") || t.Contains("grant")) return "Award/Grant";
        return "Unknown";
    }

    private static string? GetRawString(JsonElement el, string field) =>
        el.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long? ExtractRawLong(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number) return el.GetInt64();
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("raw", out var raw) && raw.ValueKind == JsonValueKind.Number)
            return raw.GetInt64();
        return null;
    }

    private static double? ExtractRawDouble(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number) return el.GetDouble();
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("raw", out var raw) && raw.ValueKind == JsonValueKind.Number)
            return raw.GetDouble();
        return null;
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
