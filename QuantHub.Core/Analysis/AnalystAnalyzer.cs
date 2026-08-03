using System.Text.Json;
using QuantHub.Core.Models;
using QuantHub.Core.Yahoo;

namespace QuantHub.Core.Analysis;

/// <summary>Ports the analyst command (stock_data.py lines 516-588), including the consensus-rating
/// title-casing. The original's recommendation-trend period labeling had a dead "Current" branch -
/// Yahoo's period values are "0m"/"-1m"/"-2m"/"-3m", and the original only special-cased a leading
/// "-", so "0m" (the current month) fell through to the raw string instead of "Current" - fixed here
/// to parse the magnitude regardless of sign.
///
/// Yahoo's public upgradeDowngradeHistory payload does not reliably expose a price-target delta per
/// action, so PriceTargetAction/CurrentPriceTarget/PriorPriceTarget are left null here - the schema
/// already models them as optional/nullable for exactly this reason.</summary>
public static class AnalystAnalyzer
{
    public static readonly string[] Modules = ["financialData", "recommendationTrend", "upgradeDowngradeHistory"];

    public static AnalystData Build(string ticker, JsonElement result)
    {
        var recKeyRaw = YahooJson.Str(result, "financialData", "recommendationKey");
        var consensus = string.IsNullOrEmpty(recKeyRaw) ? "N/A" : TitleCase(recKeyRaw.Replace('_', ' '));

        var numAnalysts = YahooJson.Raw(result, "financialData", "numberOfAnalystOpinions");
        var currentPrice = YahooJson.Raw(result, "financialData", "currentPrice");
        var targetLow = YahooJson.Raw(result, "financialData", "targetLowPrice");
        var targetMean = YahooJson.Raw(result, "financialData", "targetMeanPrice");
        var targetHigh = YahooJson.Raw(result, "financialData", "targetHighPrice");

        var recentActions = new List<AnalystAction>();
        if (result.TryGetProperty("upgradeDowngradeHistory", out var udh) &&
            udh.TryGetProperty("history", out var history) && history.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in history.EnumerateArray().Take(40))
            {
                string? date = null;
                if (row.TryGetProperty("epochGradeDate", out var epoch) && epoch.ValueKind == JsonValueKind.Number)
                {
                    date = DateTimeOffset.FromUnixTimeSeconds(epoch.GetInt64()).UtcDateTime.ToString("yyyy-MM-dd");
                }

                recentActions.Add(new AnalystAction
                {
                    Firm = GetString(row, "firm") ?? "",
                    ToGrade = NullIfEmpty(GetString(row, "toGrade")),
                    FromGrade = NullIfEmpty(GetString(row, "fromGrade")),
                    Date = date,
                    Action = NullIfEmpty(GetString(row, "action")) ?? "reiterated",
                    PriceTargetAction = null,
                    CurrentPriceTarget = null,
                    PriorPriceTarget = null
                });
            }
        }

        var trend = new List<RecommendationTrendPoint>();
        if (result.TryGetProperty("recommendationTrend", out var rt) &&
            rt.TryGetProperty("trend", out var trendArr) && trendArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in trendArr.EnumerateArray())
            {
                var periodRaw = GetString(row, "period") ?? "";
                var monthsAgo = int.TryParse(periodRaw.TrimStart('-').Replace("m", ""), out var m) ? m : (int?)null;
                var label = monthsAgo switch
                {
                    0 => "Current",
                    { } n => $"{n}mo ago",
                    null => periodRaw
                };

                trend.Add(new RecommendationTrendPoint
                {
                    Period = label,
                    StrongBuy = GetInt(row, "strongBuy") ?? 0,
                    Buy = GetInt(row, "buy") ?? 0,
                    Hold = GetInt(row, "hold") ?? 0,
                    Sell = GetInt(row, "sell") ?? 0,
                    StrongSell = GetInt(row, "strongSell") ?? 0
                });
            }
        }

        return new AnalystData
        {
            Ticker = ticker.ToUpperInvariant(),
            ConsensusRating = consensus,
            NumAnalysts = numAnalysts is { } na ? (int)na : null,
            CurrentPrice = currentPrice,
            TargetLow = targetLow,
            TargetMean = targetMean,
            TargetHigh = targetHigh,
            RecentActions = recentActions,
            RecommendationTrend = trend
        };
    }

    /// <summary>Percent upside (positive) or downside (negative) the average analyst price target
    /// implies versus the current price - null if either input is missing or the price is non-positive
    /// (a degenerate base for a percentage). Shared by AnalystViewModel's Beginner summary, the
    /// Watchlist ranking table, and the Universe Top 20 ranking, so all three read "upside" the same
    /// way.</summary>
    public static double? UpsidePotentialPct(double? targetMean, double? currentPrice) =>
        targetMean is { } target && currentPrice is { } price && price > 0
            ? (target - price) / price * 100
            : null;

    /// <summary>Ordinal "best to buy first" rank for a consensus rating string - lower is more bullish.
    /// Unrecognized values (including Yahoo's own "N/A" fallback and a missing/null rating) sort last
    /// rather than erroring, so a ticker with no analyst coverage still has a well-defined position at
    /// the bottom of an AnalystRating-sorted list instead of being silently dropped.</summary>
    public static int ConsensusRatingRank(string? consensusRating) => consensusRating switch
    {
        "Strong Buy" => 0,
        "Buy" => 1,
        "Hold" => 2,
        "Sell" => 3,
        "Strong Sell" => 4,
        _ => 5
    };

    private static string? GetString(JsonElement el, string field) =>
        el.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? GetInt(JsonElement el, string field) =>
        el.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    /// <summary>Matches Python's str.title(): uppercase each word's first letter, lowercase the rest.</summary>
    internal static string TitleCase(string s)
    {
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }
}
