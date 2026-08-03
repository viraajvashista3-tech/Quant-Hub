using System.IO;
using System.Text.Json;
using QuantHub.Core.Backtesting;
using QuantHub.Core.Portfolio;
using QuantHub.Core.Services;

namespace QuantHub.Desktop.Services;

/// <summary>Persists user-entered positions (%LOCALAPPDATA%\QuantHub\portfolio.json, mirroring
/// WatchlistService's pattern) and orchestrates the network calls PortfolioCalculator needs: the
/// benchmark's (SPY) price on the entry date when a position is added, and current prices for both
/// the ticker and SPY when performance is evaluated.</summary>
public sealed class PortfolioService
{
    private readonly StockAnalysisService _stockAnalysis;
    private readonly string _path;
    private readonly List<Position> _positions;

    public event EventHandler? Changed;

    public PortfolioService(StockAnalysisService stockAnalysis)
        : this(stockAnalysis, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuantHub"))
    {
    }

    /// <summary>Lets callers (tests) point persistence at a directory other than the real
    /// %LOCALAPPDATA%\QuantHub, so exercising Add/Remove/Load doesn't touch a real machine's file -
    /// same pattern as WatchlistService/ScoreWeightsService's test-friendly constructors.</summary>
    public PortfolioService(StockAnalysisService stockAnalysis, string dataDirectory)
    {
        _stockAnalysis = stockAnalysis;
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "portfolio.json");
        _positions = Load();
    }

    public IReadOnlyList<Position> Positions => _positions;

    /// <summary>Fetches SPY's price on/before entryDate (5y of history covers any realistic manually-
    /// entered position) before appending - a network call, unlike WatchlistService.Add, since
    /// PortfolioCalculator needs an entry-time benchmark price to ever compute excess return for this
    /// position. Returns false (nothing added) if that lookup fails, rather than silently storing a
    /// position that could never be evaluated against the benchmark.</summary>
    public async Task<bool> AddPositionAsync(string ticker, double shares, double entryPrice, DateOnly entryDate, CancellationToken ct = default)
    {
        var upper = ticker.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(upper) || shares <= 0 || entryPrice <= 0) return false;

        var benchmarkHistory = await _stockAnalysis.GetHistoryAsync(BacktestEngine.BenchmarkTicker, "5y", ct);
        if (benchmarkHistory is not { Bars.Count: > 0 }) return false;
        var benchmarkPrice = PortfolioCalculator.PriceOnOrBefore(benchmarkHistory.Bars, entryDate);
        if (benchmarkPrice is null) return false;

        _positions.Add(new Position(upper, shares, entryPrice, entryDate, benchmarkPrice.Value));
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Removes the first position matching both ticker and entry date - not ticker alone,
    /// since the same ticker can legitimately be bought in more than one lot at different times/
    /// prices, and those need to stay distinguishable.</summary>
    public void RemovePosition(string ticker, DateOnly entryDate)
    {
        var upper = ticker.ToUpperInvariant();
        var index = _positions.FindIndex(p => p.Ticker == upper && p.EntryDate == entryDate);
        if (index < 0) return;
        _positions.RemoveAt(index);
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Fetches current prices for every distinct held ticker (plus the benchmark once) in
    /// parallel and evaluates each position - never throws; a ticker whose current-price fetch fails
    /// is simply omitted from the result rather than failing the whole page.</summary>
    public async Task<IReadOnlyList<PositionPerformance>> EvaluateAllAsync(CancellationToken ct = default)
    {
        var positions = _positions.ToList();
        if (positions.Count == 0) return [];

        var currentBenchmark = await GetCurrentPriceAsync(BacktestEngine.BenchmarkTicker, ct);
        if (currentBenchmark is null) return [];

        var priceByTicker = new Dictionary<string, double>();
        var gate = new object();
        await Parallel.ForEachAsync(positions.Select(p => p.Ticker).Distinct(), ct, async (ticker, token) =>
        {
            var price = await GetCurrentPriceAsync(ticker, token);
            if (price is { } p) lock (gate) priceByTicker[ticker] = p;
        });

        return positions
            .Where(p => priceByTicker.ContainsKey(p.Ticker))
            .Select(p => PortfolioCalculator.Evaluate(p, priceByTicker[p.Ticker], currentBenchmark.Value))
            .ToList();
    }

    private async Task<double?> GetCurrentPriceAsync(string ticker, CancellationToken ct)
    {
        try
        {
            // "ytd" is the smallest valid period guaranteed to include today's bar - same choice
            // PredictionLogService makes for the same reason.
            var history = await _stockAnalysis.GetHistoryAsync(ticker, "ytd", ct);
            return history is { Bars.Count: > 0 } h ? h.Bars[^1].Close : null;
        }
        catch
        {
            return null;
        }
    }

    private List<Position> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                if (JsonSerializer.Deserialize<List<Position>>(json) is { } loaded) return loaded;
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
            File.WriteAllText(_path, JsonSerializer.Serialize(_positions, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // best-effort persistence; not fatal if it fails
        }
    }
}
