using System.Text.Json;
using QuantHub.Core.Models;

namespace QuantHub.Core.Analysis;

/// <summary>Parses Yahoo's earningsHistory (last ~4 reported quarters' actual vs. estimated EPS and
/// the resulting surprise%) and calendarEvents (next earnings date, next ex-dividend date) modules -
/// both previously unused by this app. Feeds the Fundamentals tab's new Earnings card, and (see
/// QuantScoreCalculator.EarningsSurpriseSignal/BacktestEngine) the candidate post-earnings-
/// announcement-drift score factor.</summary>
public static class EarningsAnalyzer
{
    public static readonly string[] Modules = ["earningsHistory", "calendarEvents"];

    public static EarningsData Build(string ticker, JsonElement result)
    {
        var upper = ticker.ToUpperInvariant();
        var history = new List<EarningsQuarter>();

        if (result.TryGetProperty("earningsHistory", out var ehMod) &&
            ehMod.TryGetProperty("history", out var histArr) && histArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in histArr.EnumerateArray().Take(4))
            {
                string? date = null;
                if (row.TryGetProperty("quarter", out var q) && ExtractRawLong(q) is { } epoch)
                    date = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime.ToString("yyyy-MM-dd");

                history.Add(new EarningsQuarter
                {
                    Date = date ?? "",
                    EpsActual = row.TryGetProperty("epsActual", out var ea) ? ExtractRawDouble(ea) : null,
                    EpsEstimate = row.TryGetProperty("epsEstimate", out var ee) ? ExtractRawDouble(ee) : null,
                    SurprisePercent = row.TryGetProperty("surprisePercent", out var sp) ? ExtractRawDouble(sp) : null
                });
            }
        }
        // Yahoo returns history oldest-first in some responses, newest-first in others - sort
        // explicitly so callers (and the score factor below) can always trust index 0 = most recent.
        history = history.Where(h => h.Date.Length > 0).OrderByDescending(h => h.Date, StringComparer.Ordinal).ToList();

        string? nextEarningsDate = null;
        string? exDividendDate = null;
        if (result.TryGetProperty("calendarEvents", out var ceMod))
        {
            if (ceMod.TryGetProperty("earnings", out var earnings) &&
                earnings.TryGetProperty("earningsDate", out var edArr) && edArr.ValueKind == JsonValueKind.Array &&
                edArr.GetArrayLength() > 0 && ExtractRawLong(edArr[0]) is { } nextEpoch)
            {
                nextEarningsDate = DateTimeOffset.FromUnixTimeSeconds(nextEpoch).UtcDateTime.ToString("yyyy-MM-dd");
            }
            if (ceMod.TryGetProperty("exDividendDate", out var exDiv) && ExtractRawLong(exDiv) is { } exEpoch)
            {
                exDividendDate = DateTimeOffset.FromUnixTimeSeconds(exEpoch).UtcDateTime.ToString("yyyy-MM-dd");
            }
        }

        return new EarningsData
        {
            Ticker = upper,
            History = history,
            NextEarningsDate = nextEarningsDate,
            ExDividendDate = exDividendDate
        };
    }

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
}
