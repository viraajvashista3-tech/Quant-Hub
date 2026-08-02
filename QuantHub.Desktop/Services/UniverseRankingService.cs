using System.IO;
using System.Text.Json;
using QuantHub.Core.Analysis;
using QuantHub.Core.Models;
using QuantHub.Core.Services;
using QuantHub.Core.Universe;

namespace QuantHub.Desktop.Services;

/// <summary>Keeps a full-universe (UniverseData.AllTickers, the same 138 tickers
/// BacktestEngine/AutoBacktestService already sweep) ranking fresh without blocking the Universe
/// page's Top 20 table on a live 2-calls-per-ticker sweep every time it's opened. Checks once per app launch
/// whether the cached ranking is more than RecheckInterval old and, if so, re-sweeps in the
/// background - same "fire and forget if due, persist timestamp + result to JSON" shape as
/// AutoBacktestService, just roughly daily instead of weekly (a full-universe QuantScore/analyst
/// sweep is worth refreshing more often than a 5-year-history backtest recalibration).
///
/// Two separate JSON files, two separate lifetimes: universeranking.json holds only the single most
/// recent sweep ("Current" - what the Top 20 table actually reads on page load), while
/// universerankinghistory.json holds the small (at most 2 entries) monthly archive
/// UniverseRanking.UpdateMonthlyArchive maintains, which is what "this month vs last month" compares
/// against. Current updates every sweep; the monthly archive only gains a new entry on the first
/// sweep of a new calendar month - see UniverseRanking's own remarks for why.</summary>
public sealed class UniverseRankingService
{
    private static readonly TimeSpan RecheckInterval = TimeSpan.FromHours(20);

    /// <summary>Caps concurrent per-ticker fetches during a sweep - same reasoning and value as
    /// BacktestEngine.MaxNetworkConcurrency (Yahoo's endpoint is unauthenticated/undocumented, so an
    /// uncapped 138-ticker x 2-call burst risks transient failures showing up as inflated skips
    /// rather than an actual rate limit ever being documented anywhere to size against precisely).</summary>
    private const int MaxNetworkConcurrency = 16;

    private readonly StockAnalysisService _stockAnalysis;
    private readonly ScoreWeightsService _scoreWeights;
    private readonly string _currentPath;
    private readonly string _historyPath;

    public DateTime? LastRunUtc { get; private set; }
    public bool IsSweeping { get; private set; }
    public UniverseSnapshot? Current { get; private set; }
    public IReadOnlyDictionary<string, UniverseSnapshot> MonthlyArchive { get; private set; } =
        new Dictionary<string, UniverseSnapshot>();

    /// <summary>Raised whenever a sweep starts or finishes, so the Universe page can show a
    /// sweeping indicator and refresh its Top 20 table the instant new data lands, without polling.</summary>
    public event EventHandler? StateChanged;

    public UniverseRankingService(StockAnalysisService stockAnalysis, ScoreWeightsService scoreWeights)
        : this(stockAnalysis, scoreWeights,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuantHub"))
    {
    }

    /// <summary>Lets callers (tests) point persistence at a directory other than the real
    /// %LOCALAPPDATA%\QuantHub, mirroring WatchlistService/ScoreWeightsService's same test seam.</summary>
    public UniverseRankingService(StockAnalysisService stockAnalysis, ScoreWeightsService scoreWeights, string dataDirectory)
    {
        _stockAnalysis = stockAnalysis;
        _scoreWeights = scoreWeights;

        Directory.CreateDirectory(dataDirectory);
        _currentPath = Path.Combine(dataDirectory, "universeranking.json");
        _historyPath = Path.Combine(dataDirectory, "universerankinghistory.json");

        var state = LoadCurrent();
        LastRunUtc = state?.LastRunUtc;
        Current = state?.Current;
        MonthlyArchive = LoadHistory();
    }

    /// <summary>Fire-and-forget: call once at startup. No-ops if the last sweep is still within
    /// RecheckInterval.</summary>
    public void RunInBackgroundIfDue() => _ = RunIfDueAsync();

    private Task RunIfDueAsync() =>
        LastRunUtc is { } last && DateTime.UtcNow - last < RecheckInterval
            ? Task.CompletedTask
            : RunNowAsync();

    /// <summary>The Top N tickers (best-to-buy-first) for the given metric, read from the already-
    /// swept Current snapshot - never triggers a network call itself. Empty before the first sweep
    /// has ever completed.</summary>
    public IReadOnlyList<TickerRankData> GetTopN(RankingMetric metric, int n = 20) =>
        Current is { } c ? UniverseRanking.TopN(c.Tickers, metric, n) : [];

    /// <summary>What changed in the Top N (by the given metric) since last month, and why - empty
    /// before two distinct calendar months have been swept at least once each.</summary>
    public IReadOnlyList<TickerRankChange> ExplainChanges(RankingMetric metric, int n = 20) =>
        Current is { } c
            ? UniverseRanking.ExplainTopNChanges(UniverseRanking.PreviousMonthSnapshot(MonthlyArchive, DateTime.UtcNow), c, metric, n)
            : [];

    /// <summary>Also callable directly (e.g. the shell's Refresh button) to force an out-of-schedule
    /// sweep. Sweeps the full universe in parallel (same Parallel.ForEachAsync shape as
    /// BacktestEngine.RunAsync's phase 1), fetching each ticker's overview (for QuantScore) and a
    /// best-effort analyst lookup (for Upside/Rating) side by side.</summary>
    public async Task RunNowAsync(CancellationToken ct = default)
    {
        IsSweeping = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            var tickers = UniverseData.AllTickers;
            var rows = new List<TickerRankData>();
            var skipped = new List<string>();
            var gate = new object();

            var networkOptions = new ParallelOptions { MaxDegreeOfParallelism = MaxNetworkConcurrency, CancellationToken = ct };
            await Parallel.ForEachAsync(tickers, networkOptions, async (ticker, token) =>
            {
                var overviewTask = _stockAnalysis.GetOverviewAsync(ticker, _scoreWeights.Current, token);
                var analystTask = FetchAnalystBestEffortAsync(ticker, token);
                await Task.WhenAll(overviewTask, analystTask);

                var overview = overviewTask.Result;
                if (overview is null)
                {
                    lock (gate) skipped.Add(ticker);
                    return;
                }

                var analyst = analystTask.Result;
                var row = new TickerRankData(
                    overview.Ticker, overview.Name, overview.Price, overview.QuantScore, overview.Signal,
                    overview.TrendScore, overview.MomentumScore, overview.MacdScore, overview.VolScore,
                    overview.MeanReversionScore, overview.PriceMomentumScore, overview.SentimentContrib,
                    TargetMean: analyst?.TargetMean,
                    UpsidePotentialPct: AnalystAnalyzer.UpsidePotentialPct(analyst?.TargetMean, analyst?.CurrentPrice ?? overview.Price),
                    ConsensusRating: analyst?.ConsensusRating,
                    AnalystRatingRank: AnalystAnalyzer.ConsensusRatingRank(analyst?.ConsensusRating));
                lock (gate) rows.Add(row);
            });

            var snapshot = new UniverseSnapshot(DateTime.UtcNow, rows, skipped);
            MonthlyArchive = UniverseRanking.UpdateMonthlyArchive(MonthlyArchive, snapshot);
            Current = snapshot;
            LastRunUtc = DateTime.UtcNow;
            SaveCurrent();
            SaveHistory();
        }
        catch (OperationCanceledException)
        {
            // app shutting down mid-sweep - retried on next launch/RunInBackgroundIfDue check
        }
        catch
        {
            // best-effort - a network hiccup shouldn't crash startup or block the Universe page
        }
        finally
        {
            IsSweeping = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Best-effort analyst-coverage fetch - degrades to null on any failure (network hiccup,
    /// or a ticker with genuinely no analyst coverage), same shape as
    /// BacktestEngine.FetchInsiderPurchaseDatesAsync / TerminalViewModel.FetchAnalystBestEffortAsync.</summary>
    private async Task<AnalystData?> FetchAnalystBestEffortAsync(string ticker, CancellationToken ct)
    {
        try
        {
            return await _stockAnalysis.GetAnalystAsync(ticker, ct);
        }
        catch
        {
            return null;
        }
    }

    private CurrentState? LoadCurrent()
    {
        try
        {
            if (File.Exists(_currentPath))
            {
                var json = File.ReadAllText(_currentPath);
                return JsonSerializer.Deserialize<CurrentState>(json);
            }
        }
        catch
        {
            // corrupt or unreadable file - treat as "never run" rather than crash startup
        }
        return null;
    }

    private void SaveCurrent()
    {
        try
        {
            var json = JsonSerializer.Serialize(new CurrentState(LastRunUtc, Current), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_currentPath, json);
        }
        catch
        {
            // best-effort persistence; not fatal if it fails
        }
    }

    private Dictionary<string, UniverseSnapshot> LoadHistory()
    {
        try
        {
            if (File.Exists(_historyPath))
            {
                var json = File.ReadAllText(_historyPath);
                if (JsonSerializer.Deserialize<Dictionary<string, UniverseSnapshot>>(json) is { } loaded) return loaded;
            }
        }
        catch
        {
            // corrupt or unreadable file - start with an empty archive rather than crash startup
        }
        return [];
    }

    private void SaveHistory()
    {
        try
        {
            var json = JsonSerializer.Serialize(MonthlyArchive, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyPath, json);
        }
        catch
        {
            // best-effort persistence; not fatal if it fails
        }
    }

    private sealed record CurrentState(DateTime? LastRunUtc, UniverseSnapshot? Current);
}
