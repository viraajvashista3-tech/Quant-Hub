using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace QuantHub.Core.Yahoo;

public sealed record Bar(DateOnly Date, double Open, double High, double Low, double Close, long Volume);

/// <summary>
/// Direct HTTP client against Yahoo Finance's public JSON endpoints - the same ones yfinance
/// itself wraps (v8 chart, v10 quoteSummary). No Python/yfinance dependency at runtime.
///
/// Yahoo requires a session cookie plus a "crumb" token on every request; this negotiates and
/// caches both, refreshing once on a 401/403 in case the crumb expired mid-session. This
/// handshake is the single piece of this client most likely to need adjustment if Yahoo changes
/// its anti-bot flow - see the Phase 1 smoke test that exercises it against live data.
/// </summary>
public sealed class YahooFinanceClient(HttpClient http)
{
    private string? _crumb;
    private readonly SemaphoreSlim _crumbLock = new(1, 1);

    // Short-TTL response cache: the real cost this addresses is switching between pages for the
    // *same* active ticker (Terminal -> Fundamentals -> Analyst -> Peers -> Insider each
    // independently re-fetch chart/quoteSummary data for whatever ticker is currently active), not
    // long-lived staleness. 60s is short enough that the shell's explicit Refresh button still feels
    // meaningfully fresh in virtually every realistic click pattern, while being long enough to make
    // rapid same-ticker page-switching feel instant instead of re-fetching from scratch every time.
    // Only successful (non-null) responses are cached - a transient failure is never "stuck" for the
    // TTL window, it just retries on the very next call.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private readonly ConcurrentDictionary<string, (DateTime ExpiresAtUtc, IReadOnlyList<Bar> Value)> _chartCache = new();
    private readonly ConcurrentDictionary<string, (DateTime ExpiresAtUtc, JsonElement Value)> _quoteSummaryCache = new();

    public static HttpClient CreateDefaultHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.All
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        return client;
    }

    private async Task EnsureCrumbAsync(CancellationToken ct)
    {
        if (_crumb is not null) return;
        await _crumbLock.WaitAsync(ct);
        try
        {
            if (_crumb is not null) return;
            await NegotiateCrumbAsync(ct);
        }
        finally
        {
            _crumbLock.Release();
        }
    }

    private async Task NegotiateCrumbAsync(CancellationToken ct)
    {
        try
        {
            using var warmup = await http.GetAsync("https://fc.yahoo.com", ct);
        }
        catch
        {
            // best-effort cookie warmup; the crumb request below still has a chance without it
        }

        var crumbResp = await http.GetAsync("https://query2.finance.yahoo.com/v1/test/getcrumb", ct);
        if (crumbResp.IsSuccessStatusCode)
        {
            _crumb = (await crumbResp.Content.ReadAsStringAsync(ct)).Trim().Trim('"');
        }
    }

    private async Task<HttpResponseMessage> GetWithCrumbAsync(string urlWithoutCrumb, CancellationToken ct)
    {
        await EnsureCrumbAsync(ct);
        var url = _crumb is { Length: > 0 } c ? $"{urlWithoutCrumb}&crumb={Uri.EscapeDataString(c)}" : urlWithoutCrumb;
        var resp = await http.GetAsync(url, ct);
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _crumb = null;
            await EnsureCrumbAsync(ct);
            url = _crumb is { Length: > 0 } c2 ? $"{urlWithoutCrumb}&crumb={Uri.EscapeDataString(c2)}" : urlWithoutCrumb;
            resp.Dispose();
            resp = await http.GetAsync(url, ct);
        }
        return resp;
    }

    /// <summary>range: "ytd" | "6mo" | "1y" | "2y" | "5y" | "1mo" (matching Yahoo's chart range values).</summary>
    public async Task<IReadOnlyList<Bar>?> GetChartAsync(string symbol, string range, CancellationToken ct = default)
    {
        var cacheKey = $"{symbol}:{range}";
        if (_chartCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow)
            return cached.Value;

        var bars = await FetchChartAsync(symbol, range, ct);
        if (bars is not null) _chartCache[cacheKey] = (DateTime.UtcNow + CacheTtl, bars);
        return bars;
    }

    private async Task<IReadOnlyList<Bar>?> FetchChartAsync(string symbol, string range, CancellationToken ct)
    {
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?range={range}&interval=1d";
        using var resp = await GetWithCrumbAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("chart", out var chart)) return null;
        if (!chart.TryGetProperty("result", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) return null;

        var result = results[0];
        if (!result.TryGetProperty("timestamp", out var tsEl) || tsEl.ValueKind != JsonValueKind.Array) return [];
        var timestamps = tsEl.EnumerateArray().Select(t => t.GetInt64()).ToArray();

        var indicators = result.GetProperty("indicators");
        var quote = indicators.GetProperty("quote")[0];
        var opens = ReadDoubleArray(quote, "open", timestamps.Length);
        var highs = ReadDoubleArray(quote, "high", timestamps.Length);
        var lows = ReadDoubleArray(quote, "low", timestamps.Length);
        var closes = ReadDoubleArray(quote, "close", timestamps.Length);
        var volumes = ReadLongArray(quote, "volume", timestamps.Length);

        double?[]? adjCloses = null;
        if (indicators.TryGetProperty("adjclose", out var adjArr) && adjArr.ValueKind == JsonValueKind.Array && adjArr.GetArrayLength() > 0)
        {
            adjCloses = ReadDoubleArray(adjArr[0], "adjclose", timestamps.Length);
        }

        var bars = new List<Bar>(timestamps.Length);
        for (var i = 0; i < timestamps.Length; i++)
        {
            if (closes[i] is not { } close || close == 0) continue;
            var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(timestamps[i]).UtcDateTime);
            var ratio = adjCloses is not null && adjCloses[i] is { } adjClose ? adjClose / close : 1.0;
            bars.Add(new Bar(
                date,
                (opens[i] ?? close) * ratio,
                (highs[i] ?? close) * ratio,
                (lows[i] ?? close) * ratio,
                close * ratio,
                volumes[i] ?? 0));
        }
        return bars;
    }

    /// <summary>Ticker/company-name search-as-you-type (v1/finance/search) - returns raw {"quotes":
    /// [...]} JSON for TickerSearchParser.Parse to filter/map. A blank query still round-trips to
    /// Yahoo (unlike StockAnalysisService.SearchTickersAsync, which short-circuits before calling this
    /// at all) since this client is a thin transport layer with no query-shape opinions of its own,
    /// matching GetChartAsync/GetQuoteSummaryAsync.</summary>
    public async Task<JsonElement?> SearchAsync(string query, CancellationToken ct = default)
    {
        var url = $"https://query1.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(query)}&quotesCount=8&newsCount=0";
        using var resp = await GetWithCrumbAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.Clone();
    }

    /// <summary>Returns the first (and only) quoteSummary result object, containing every requested module keyed by module name.</summary>
    public async Task<JsonElement?> GetQuoteSummaryAsync(string symbol, IEnumerable<string> modules, CancellationToken ct = default)
    {
        // Materialized once (not just enumerated) since it's read twice below (cache key + fetch) -
        // a lazily-evaluated caller-supplied IEnumerable could otherwise yield different results, or
        // nothing at all, on its second enumeration.
        var moduleList = modules as IReadOnlyList<string> ?? modules.ToList();
        var cacheKey = $"{symbol}:{string.Join(",", moduleList.OrderBy(m => m, StringComparer.Ordinal))}";
        if (_quoteSummaryCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow)
            return cached.Value;

        var result = await FetchQuoteSummaryAsync(symbol, moduleList, ct);
        if (result is { } r) _quoteSummaryCache[cacheKey] = (DateTime.UtcNow + CacheTtl, r);
        return result;
    }

    private async Task<JsonElement?> FetchQuoteSummaryAsync(string symbol, IReadOnlyList<string> modules, CancellationToken ct)
    {
        var moduleParam = string.Join(",", modules);
        var url = $"https://query1.finance.yahoo.com/v10/finance/quoteSummary/{Uri.EscapeDataString(symbol)}?modules={moduleParam}";
        using var resp = await GetWithCrumbAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("quoteSummary", out var qs)) return null;
        if (!qs.TryGetProperty("result", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) return null;
        return results[0].Clone();
    }

    private static double?[] ReadDoubleArray(JsonElement quote, string field, int length)
    {
        var result = new double?[length];
        if (!quote.TryGetProperty(field, out var arr) || arr.ValueKind != JsonValueKind.Array) return result;
        var i = 0;
        foreach (var el in arr.EnumerateArray())
        {
            if (i >= length) break;
            result[i] = el.ValueKind == JsonValueKind.Number ? el.GetDouble() : null;
            i++;
        }
        return result;
    }

    private static long?[] ReadLongArray(JsonElement quote, string field, int length)
    {
        var result = new long?[length];
        if (!quote.TryGetProperty(field, out var arr) || arr.ValueKind != JsonValueKind.Array) return result;
        var i = 0;
        foreach (var el in arr.EnumerateArray())
        {
            if (i >= length) break;
            result[i] = el.ValueKind == JsonValueKind.Number ? el.GetInt64() : null;
            i++;
        }
        return result;
    }
}
