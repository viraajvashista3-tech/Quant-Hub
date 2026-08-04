using System.IO;
using System.Text.Json;
using QuantHub.Core.Alerts;
using QuantHub.Core.Services;

namespace QuantHub.Desktop.Services;

/// <summary>Persists user-set price alerts (%LOCALAPPDATA%\QuantHub\alerts.json, mirroring
/// WatchlistService's pattern) and checks them two ways: opportunistically for free whenever a page
/// already fetched a ticker's current price (CheckTicker - no extra network call), and via a full
/// background sweep at app startup (CheckAllInBackground, mirroring AutoBacktestService/
/// PredictionLogService's fire-and-forget startup pattern) for tickers not currently being viewed.
/// Deliberately does not run its own polling timer - piggybacking on page loads keeps this feature
/// from adding a permanently-running network loop.</summary>
public sealed class AlertService
{
    private readonly StockAnalysisService _stockAnalysis;
    private readonly string _path;
    private readonly List<PriceAlert> _alerts;
    private readonly object _gate = new();

    /// <summary>Raised whenever one or more alerts newly trigger, from either CheckTicker or
    /// CheckAllAsync - the Shell subscribes to fold these into the sidebar's "what changed" banner
    /// alongside watchlist signal changes.</summary>
    public event EventHandler<IReadOnlyList<PriceAlert>>? Triggered;

    public AlertService(StockAnalysisService stockAnalysis)
        : this(stockAnalysis, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuantHub"))
    {
    }

    /// <summary>Lets callers (tests) point persistence at a directory other than the real
    /// %LOCALAPPDATA%\QuantHub, so exercising Add/Remove/Check doesn't touch a real machine's file -
    /// same pattern as WatchlistService/PortfolioService's test-friendly constructors.</summary>
    public AlertService(StockAnalysisService stockAnalysis, string dataDirectory)
    {
        _stockAnalysis = stockAnalysis;
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "alerts.json");
        _alerts = Load();
    }

    public IReadOnlyList<PriceAlert> Alerts { get { lock (_gate) return _alerts.ToList(); } }

    public IReadOnlyList<PriceAlert> ActiveAlertsFor(string ticker)
    {
        var upper = ticker.ToUpperInvariant();
        lock (_gate) return _alerts.Where(a => a.Ticker == upper && a.TriggeredAtUtc is null).ToList();
    }

    public void AddAlert(string ticker, AlertDirection direction, double targetPrice)
    {
        var upper = ticker.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(upper) || targetPrice <= 0) return;

        lock (_gate) _alerts.Add(new PriceAlert(Guid.NewGuid(), upper, direction, targetPrice, DateTime.UtcNow, null, null));
        Save();
    }

    public void RemoveAlert(Guid id)
    {
        lock (_gate)
        {
            var index = _alerts.FindIndex(a => a.Id == id);
            if (index < 0) return;
            _alerts.RemoveAt(index);
        }
        Save();
    }

    /// <summary>Evaluates this ticker's active alerts against a price the caller already has in hand
    /// (e.g. TerminalViewModel's just-loaded Overview.Price) - no network call of its own. Synchronous
    /// and cheap enough to call on every page load without worrying about it.</summary>
    public void CheckTicker(string ticker, double currentPrice)
    {
        var upper = ticker.ToUpperInvariant();
        List<PriceAlert> newlyTriggered = [];
        lock (_gate)
        {
            for (var i = 0; i < _alerts.Count; i++)
            {
                var alert = _alerts[i];
                if (alert.Ticker != upper || alert.TriggeredAtUtc is not null) continue;
                if (!AlertEvaluator.IsTriggered(alert, currentPrice)) continue;

                var triggeredAlert = alert with { TriggeredAtUtc = DateTime.UtcNow, TriggeredAtPrice = currentPrice };
                _alerts[i] = triggeredAlert;
                newlyTriggered.Add(triggeredAlert);
            }
        }
        if (newlyTriggered.Count == 0) return;
        Save();
        Triggered?.Invoke(this, newlyTriggered);
    }

    /// <summary>Fire-and-forget: call once at startup, same pattern as AutoBacktestService/
    /// PredictionLogService. No-ops (near-instantly) if there are no active alerts at all.</summary>
    public void CheckAllInBackground() => _ = CheckAllAsync();

    public async Task CheckAllAsync(CancellationToken ct = default)
    {
        List<string> tickers;
        lock (_gate) tickers = _alerts.Where(a => a.TriggeredAtUtc is null).Select(a => a.Ticker).Distinct().ToList();
        if (tickers.Count == 0) return;

        var priceByTicker = new Dictionary<string, double>();
        var priceGate = new object();
        await Parallel.ForEachAsync(tickers, ct, async (ticker, token) =>
        {
            try
            {
                var history = await _stockAnalysis.GetHistoryAsync(ticker, "ytd", token);
                if (history is { Bars.Count: > 0 } h) lock (priceGate) priceByTicker[ticker] = h.Bars[^1].Close;
            }
            catch
            {
                // best-effort - a ticker whose fetch fails is just skipped this pass, retried next time
            }
        });

        foreach (var (ticker, price) in priceByTicker) CheckTicker(ticker, price);
    }

    private List<PriceAlert> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                if (JsonSerializer.Deserialize<List<PriceAlert>>(json) is { } loaded) return loaded;
            }
        }
        catch
        {
            // corrupt or unreadable file - start empty rather than crash startup
        }
        return [];
    }

    private void Save()
    {
        try
        {
            List<PriceAlert> snapshot;
            lock (_gate) snapshot = _alerts.ToList();
            File.WriteAllText(_path, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // best-effort persistence; not fatal if it fails
        }
    }
}
