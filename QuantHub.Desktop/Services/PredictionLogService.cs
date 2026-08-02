using System.IO;
using System.Text.Json;
using QuantHub.Core.Backtesting;
using QuantHub.Core.Models;
using QuantHub.Core.Services;

namespace QuantHub.Desktop.Services;

/// <summary>Forward-only companion to BacktestEngine: instead of validating against history, this logs
/// every real Quant Score the Terminal page actually shows a user, then checks back once enough
/// calendar time has passed to see whether the stock beat or lagged SPY (the same excess-return label
/// BacktestEngine uses - see BacktestEngine.ExcessReturnPct) - mirroring the historical engine's
/// benchmark-relative methodology so the two are directly comparable. Because every entry is written
/// before its own outcome exists, this log can't suffer the lookahead or survivorship bias a historical
/// backtest is always at some risk of - it's a slower but more trustworthy accuracy signal, built up one
/// real prediction at a time.
///
/// Persisted to %LOCALAPPDATA%\QuantHub\predictions.json, mirroring AutoBacktestService/
/// ScoreWeightsService's persistence pattern.</summary>
public sealed class PredictionLogService
{
    /// <summary>Calendar days (not trading days, unlike BacktestEngine) before a logged prediction is
    /// eligible for evaluation - roughly BacktestEngine/AutoBacktestService's default 10-trading-day
    /// horizon.</summary>
    public const int MaturityDays = 14;

    private const int MaxEntries = 3000;

    private readonly StockAnalysisService _stockAnalysis;
    private readonly string _path;
    private readonly object _gate = new();
    private List<LoggedPrediction> _entries;

    /// <summary>Raised after a log write or evaluation pass actually changes persisted state, so an
    /// open Backtest page can refresh its live-track-record numbers without polling.</summary>
    public event EventHandler? Updated;

    public PredictionLogService(StockAnalysisService stockAnalysis)
    {
        _stockAnalysis = stockAnalysis;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuantHub");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "predictions.json");
        _entries = Load();
    }

    public IReadOnlyList<LoggedPrediction> Entries
    {
        get { lock (_gate) return _entries.ToList(); }
    }

    /// <summary>Fire-and-forget: call once whenever the Terminal page finishes loading an overview.</summary>
    public void LogInBackground(StockOverview overview) => _ = LogIfNewAsync(overview);

    /// <summary>No-ops if this ticker was already logged today (in-memory check, no network call) -
    /// otherwise fetches SPY's current price (needed to compute excess return once this entry matures)
    /// and appends one entry. Best-effort: a network hiccup here should never disrupt the Terminal page
    /// that triggered it.</summary>
    public async Task LogIfNewAsync(StockOverview overview, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        lock (_gate)
        {
            if (_entries.Any(e => e.Ticker == overview.Ticker && e.LoggedAtUtc.Date == today)) return;
        }

        try
        {
            var benchmarkPrice = await GetCurrentPriceAsync(BacktestEngine.BenchmarkTicker, ct);
            if (benchmarkPrice is null) return;

            var entry = new LoggedPrediction(
                overview.Ticker, DateTime.UtcNow, overview.Price, benchmarkPrice.Value,
                overview.QuantScore, overview.Signal, null, null, null,
                overview.TrendScore, overview.MomentumScore, overview.MacdScore, overview.VolScore,
                overview.MeanReversionScore, overview.PriceMomentumScore, overview.SentimentContrib);

            lock (_gate)
            {
                // Re-check under lock - a second concurrent load for the same ticker could have
                // logged while the SPY fetch above was in flight.
                if (_entries.Any(e => e.Ticker == overview.Ticker && e.LoggedAtUtc.Date == today)) return;
                _entries.Add(entry);
                TrimOldest();
            }
            Save();
            Updated?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            // page navigated away mid-fetch - not worth logging, will be re-attempted on next view
        }
        catch
        {
            // best-effort - a network hiccup shouldn't disrupt the Terminal page
        }
    }

    /// <summary>Fire-and-forget: call once at startup, same pattern as AutoBacktestService. Cheap when
    /// nothing is due (a single in-memory scan) - only fetches prices for tickers that actually have a
    /// matured, unevaluated entry.</summary>
    public void EvaluateMaturedInBackground() => _ = EvaluateMaturedAsync();

    public async Task EvaluateMaturedAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-MaturityDays);
        List<LoggedPrediction> due;
        lock (_gate) due = _entries.Where(e => e.EvaluatedAtUtc is null && e.LoggedAtUtc <= cutoff).ToList();
        if (due.Count == 0) return;

        try
        {
            var currentBenchmark = await GetCurrentPriceAsync(BacktestEngine.BenchmarkTicker, ct);
            if (currentBenchmark is null) return;

            var priceByTicker = new Dictionary<string, double>();
            var priceGate = new object();
            await Parallel.ForEachAsync(due.Select(e => e.Ticker).Distinct(), ct, async (ticker, token) =>
            {
                var price = await GetCurrentPriceAsync(ticker, token);
                if (price is { } p) lock (priceGate) priceByTicker[ticker] = p;
            });

            lock (_gate)
            {
                for (var i = 0; i < _entries.Count; i++)
                {
                    var e = _entries[i];
                    if (e.EvaluatedAtUtc is not null || e.LoggedAtUtc > cutoff) continue;
                    if (!priceByTicker.TryGetValue(e.Ticker, out var currentPrice)) continue;

                    var excessReturn = BacktestEngine.ExcessReturnPct(e.Price, currentPrice, e.BenchmarkPrice, currentBenchmark.Value);
                    bool? hit = e.Signal switch
                    {
                        Signal.Buy => excessReturn > 0,
                        Signal.Avoid => excessReturn < 0,
                        _ => null
                    };
                    _entries[i] = e with { ExcessReturnPct = excessReturn, Hit = hit, EvaluatedAtUtc = DateTime.UtcNow };
                }
            }
            Save();
            Updated?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            // app shutting down mid-evaluation - unevaluated entries just retry next launch
        }
        catch
        {
            // best-effort - a network hiccup shouldn't block startup; retried on the next launch
        }
    }

    private async Task<double?> GetCurrentPriceAsync(string ticker, CancellationToken ct)
    {
        try
        {
            // "ytd" (not a short explicit window - GetHistoryAsync only accepts the same period keys
            // the Terminal page's period tabs do) is the smallest valid period guaranteed to include
            // today's bar; only the last bar's close is actually used.
            var history = await _stockAnalysis.GetHistoryAsync(ticker, "ytd", ct);
            return history is { Bars.Count: > 0 } h ? h.Bars[^1].Close : null;
        }
        catch
        {
            return null;
        }
    }

    private void TrimOldest()
    {
        if (_entries.Count <= MaxEntries) return;
        _entries = _entries.OrderByDescending(e => e.LoggedAtUtc).Take(MaxEntries).ToList();
    }

    private List<LoggedPrediction> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                if (JsonSerializer.Deserialize<List<LoggedPrediction>>(json) is { } loaded) return loaded;
            }
        }
        catch
        {
            // corrupt or unreadable file - start with an empty log rather than crash startup
        }
        return [];
    }

    private void Save()
    {
        try
        {
            List<LoggedPrediction> snapshot;
            lock (_gate) snapshot = _entries.ToList();
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // best-effort persistence; not fatal if it fails
        }
    }
}
