using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using QuantHub.Core.Analysis;
using QuantHub.Core.Models;
using QuantHub.Core.Services;
using QuantHub.Core.Universe;
using QuantHub.Desktop.Messages;
using QuantHub.Desktop.Services;

namespace QuantHub.Desktop.ViewModels.Pages;

public sealed record WatchlistRow(
    int Rank, string Ticker, string Name, double Price, double ChangePercent,
    double QuantScore, Signal Signal,
    double? UpsidePotentialPct, string? ConsensusRating, int AnalystRatingRank);

/// <summary>One of the three ways a set of tickers can be ranked "best to buy first" - shared by the
/// Watchlist ranking pills and the Universe Top 20 ranking pills, so both mean the same thing by each
/// label. A concrete List&lt;T&gt;, not a `[...]` collection expression targeting IReadOnlyList&lt;T&gt;:
/// that exact pattern (see SettingsService.ViewModeOptions's fix) compiles to a type with no
/// reflectable indexer, so an Avalonia `{Binding Foo[n]}` binding silently resolves to null instead of
/// throwing. Not needed here anyway - the pill rows below bind the whole options list via ItemsControl,
/// never an indexer.</summary>
public sealed record RankingMetricOption(RankingMetric Metric, string Label);

public sealed record Top20Row(
    int Rank, string Ticker, string Name, double Price, double QuantScore, Signal Signal,
    double? UpsidePotentialPct, string? ConsensusRating, string? ChangeExplanation);

/// <summary>Universe page - two independent sections sharing one page: (1) six same-sector "similar
/// stocks" cards for the active ticker (reusing PeersAnalyzer's peer/sector definition via
/// StockAnalysisService.GetSimilarStocksAsync, so "similar" here means the same thing it does on the
/// Peers page), each with a quick "+" to add it straight to the watchlist; (2) the user's own
/// watchlist - tickers added via that "+" or the dedicated add box, each fetched via the same
/// StockAnalysisService.GetOverviewAsync call Terminal uses, sorted by Quant Score descending. The two
/// sections load independently (similar stocks reload on ticker change, watchlist reloads on
/// add/remove) - originally two separate pages, merged here per user request so watchlist curation and
/// peer discovery live together instead of split across nav items.</summary>
public sealed partial class UniverseViewModel : ObservableObject, IRefreshablePage
{
    private readonly AppState _appState;
    private readonly StockAnalysisService _stockAnalysis;
    private readonly WatchlistService _watchlist;
    private readonly UniverseRankingService _universeRanking;
    private readonly SessionBriefingService _sessionBriefing;

    // ---------- Similar stocks ----------

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private IReadOnlyList<SimilarStock> _similarStocks = [];

    public string ActiveTicker => _appState.ActiveTicker;

    /// <summary>Since GetSimilarStocksAsync returns same-sector peers, every card here already shares
    /// one sector - a per-sector grouping visual would have nothing to group. This aggregate ("N of M
    /// up today") is the genuinely-available signal instead: a quick sector-mood read across the same
    /// set of cards, without inventing structure the data doesn't have.</summary>
    public string? SectorMoodSummary
    {
        get
        {
            if (SimilarStocks.Count == 0) return null;
            var withChange = SimilarStocks.Where(s => s.ChangePercent is not null).ToList();
            if (withChange.Count == 0) return null;
            var up = withChange.Count(s => s.ChangePercent > 0);
            var sector = SimilarStocks[0].Sector ?? "this sector";
            return $"{up} of {withChange.Count} {sector} peers are up today.";
        }
    }

    // ---------- Watchlist ----------

    [ObservableProperty]
    private bool _isWatchlistBusy;

    [ObservableProperty]
    private string? _watchlistErrorMessage;

    [ObservableProperty]
    private IReadOnlyList<WatchlistRow> _watchlistRows = [];

    [ObservableProperty]
    private string _addTickerInput = "";

    /// <summary>True while WatchlistRows is populated from UniverseData.DefaultTickers rather than
    /// the user's own WatchlistService.Tickers, i.e. the user hasn't added anything yet.</summary>
    [ObservableProperty]
    private bool _isShowingSuggestedTickers;

    public static readonly IReadOnlyList<RankingMetricOption> RankingMetricOptions = new List<RankingMetricOption>
    {
        new(RankingMetric.QuantScore, "Quant Score"),
        new(RankingMetric.UpsidePotential, "Upside Potential"),
        new(RankingMetric.AnalystRating, "Analyst Rating")
    };

    [ObservableProperty]
    private RankingMetricOption _watchlistRankMetric;

    /// <summary>Freshly-fetched, not-yet-sorted watchlist rows - kept separately from WatchlistRows so
    /// switching the sort pill re-sorts instantly instead of re-fetching every ticker's data again.</summary>
    private List<WatchlistRow> _rawWatchlistRows = [];

    public bool HasWatchlistRows => WatchlistRows.Count > 0;

    public string WatchlistSectionTitle => IsShowingSuggestedTickers ? "POPULAR STOCKS" : "YOUR WATCHLIST";

    public string? WatchlistSummary
    {
        get
        {
            if (WatchlistRows.Count == 0) return null;
            var buyCount = WatchlistRows.Count(r => r.Signal == Signal.Buy);
            var stockWord = WatchlistRows.Count == 1 ? "stock" : "stocks";
            var verb = buyCount == 1 ? "is" : "are";
            return $"{buyCount} of {WatchlistRows.Count} watched {stockWord} {verb} showing a Buy signal today.";
        }
    }

    // ---------- Universe Top 20 ----------

    [ObservableProperty]
    private RankingMetricOption _selectedRankingMetric;

    [ObservableProperty]
    private IReadOnlyList<Top20Row> _top20Rows = [];

    [ObservableProperty]
    private IReadOnlyList<string> _top20ChangeExplanations = [];

    public bool HasTop20ChangeExplanations => Top20ChangeExplanations.Count > 0;

    public bool HasTop20Rows => Top20Rows.Count > 0;

    public bool IsUniverseSweeping => _universeRanking.IsSweeping;

    public string? Top20LastUpdatedText => _universeRanking.LastRunUtc is { } t
        ? $"Updated {t:MMM d, h:mm tt} UTC"
        : null;

    public UniverseViewModel(
        AppState appState, StockAnalysisService stockAnalysis, WatchlistService watchlist,
        UniverseRankingService universeRanking, SessionBriefingService sessionBriefing)
    {
        _appState = appState;
        _stockAnalysis = stockAnalysis;
        _watchlist = watchlist;
        _universeRanking = universeRanking;
        _sessionBriefing = sessionBriefing;
        _watchlistRankMetric = RankingMetricOptions[0];
        _selectedRankingMetric = RankingMetricOptions[0];

        _appState.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(AppState.ActiveTicker)) return;
            OnPropertyChanged(nameof(ActiveTicker));
            _ = LoadAsync();
        };
        _watchlist.Changed += (_, _) => _ = LoadWatchlistAsync();
        // Fires both when a sweep starts (to flip on the busy indicator) and when it finishes (to
        // rebuild the table from the freshly-swept Current snapshot) - RebuildTop20 itself is cheap
        // (no network), so re-running it on every StateChanged tick is fine.
        _universeRanking.StateChanged += (_, _) => RebuildTop20();

        _ = LoadAsync();
        _ = LoadWatchlistAsync();
        RebuildTop20(); // in case Current was already populated from a prior session's persisted cache
    }

    /// <summary>Backs the "Add ticker" AutoCompleteBox's AsyncPopulator, same pattern as
    /// ShellViewModel/TerminalViewModel.</summary>
    public Task<IReadOnlyList<TickerSearchResult>> SearchTickersAsync(string query, CancellationToken ct) =>
        _stockAnalysis.SearchTickersAsync(query, ct);

    [RelayCommand]
    private void AddTicker(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return;
        _watchlist.Add(symbol);
        AddTickerInput = "";
    }

    [RelayCommand]
    private void RemoveTicker(string ticker) => _watchlist.Remove(ticker);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        // Includes a forced full-universe re-sweep (not just the two page-scoped loads) so the
        // shell's Refresh button gives the Top 20 table a way to update on demand instead of only
        // ever refreshing on UniverseRankingService's own ~daily schedule.
        await Task.WhenAll(LoadAsync(), LoadWatchlistAsync(), _universeRanking.RunNowAsync());
    }

    [RelayCommand]
    private void SelectTicker(string ticker) =>
        WeakReferenceMessenger.Default.Send(new NavigateToTickerMessage(ticker));

    [RelayCommand]
    private void SelectWatchlistRankMetric(RankingMetricOption option) => WatchlistRankMetric = option;

    [RelayCommand]
    private void SelectRankingMetric(RankingMetricOption option) => SelectedRankingMetric = option;

    partial void OnWatchlistRowsChanged(IReadOnlyList<WatchlistRow> value)
    {
        OnPropertyChanged(nameof(HasWatchlistRows));
        OnPropertyChanged(nameof(WatchlistSummary));
    }

    partial void OnIsShowingSuggestedTickersChanged(bool value) => OnPropertyChanged(nameof(WatchlistSectionTitle));

    partial void OnWatchlistRankMetricChanged(RankingMetricOption value) =>
        WatchlistRows = SortWatchlistRows(_rawWatchlistRows, value.Metric);

    partial void OnSelectedRankingMetricChanged(RankingMetricOption value) => RebuildTop20();

    partial void OnTop20ChangeExplanationsChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(HasTop20ChangeExplanations));

    partial void OnTop20RowsChanged(IReadOnlyList<Top20Row> value) => OnPropertyChanged(nameof(HasTop20Rows));

    /// <summary>Rebuilds the Top 20 table and change-explanation list from whatever
    /// UniverseRankingService currently has cached - never itself triggers a network call, so
    /// switching the sort pill or reacting to a background sweep finishing is instant.</summary>
    private void RebuildTop20()
    {
        var top = _universeRanking.GetTopN(SelectedRankingMetric.Metric);
        var changesByTicker = _universeRanking.ExplainChanges(SelectedRankingMetric.Metric)
            .ToDictionary(c => c.Ticker, c => c.Explanation);

        Top20Rows = top
            .Select((t, i) => new Top20Row(
                i + 1, t.Ticker, t.Name, t.Price, t.QuantScore, t.Signal,
                t.UpsidePotentialPct, t.ConsensusRating, changesByTicker.GetValueOrDefault(t.Ticker)))
            .ToList();
        Top20ChangeExplanations = changesByTicker.Values.ToList();
        OnPropertyChanged(nameof(IsUniverseSweeping));
        OnPropertyChanged(nameof(Top20LastUpdatedText));
    }

    private CancellationTokenSource? _loadCts;

    private async Task LoadAsync()
    {
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var ct = cts.Token;

        var ticker = _appState.ActiveTicker;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _stockAnalysis.GetSimilarStocksAsync(ticker, 6, ct);
            if (ct.IsCancellationRequested) return;

            SimilarStocks = result;
            OnPropertyChanged(nameof(SectorMoodSummary));
            if (result.Count == 0) ErrorMessage = $"No similar stocks found for {ticker}.";
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer load - ignore
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested) ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            if (!ct.IsCancellationRequested) IsBusy = false;
        }
    }

    /// <summary>Pure selection logic pulled out of LoadWatchlistAsync so it's unit-testable without
    /// the network/IO the rest of the load path needs: the user's real watchlist when they've added
    /// anything, otherwise UniverseData.DefaultTickers so the page never renders a blank table on
    /// first launch (WatchlistService starts empty by design - see its doc comment).</summary>
    public static IReadOnlyList<string> ResolveWatchlistTickers(IReadOnlyList<string> watchlistTickers) =>
        watchlistTickers.Count > 0 ? watchlistTickers.ToList() : UniverseData.DefaultTickers;

    /// <summary>Pure sort+rank: reorders rows by the given metric (QuantScore descending / Upside
    /// descending with no-analyst-data rows sorting last / AnalystRating ascending-by-rank with a
    /// QuantScore tie-break), then renumbers Rank 1..N to match. Pulled out of LoadWatchlistAsync so
    /// switching the sort pill is directly unit-testable without the network/IO the rest of the load
    /// path needs, same reasoning as ResolveWatchlistTickers above.</summary>
    public static IReadOnlyList<WatchlistRow> SortWatchlistRows(IReadOnlyList<WatchlistRow> rows, RankingMetric metric)
    {
        IOrderedEnumerable<WatchlistRow> sorted = metric switch
        {
            RankingMetric.UpsidePotential => rows.OrderByDescending(r => r.UpsidePotentialPct ?? double.NegativeInfinity),
            RankingMetric.AnalystRating => rows.OrderBy(r => r.AnalystRatingRank).ThenByDescending(r => r.QuantScore),
            _ => rows.OrderByDescending(r => r.QuantScore)
        };
        return sorted.Select((r, i) => r with { Rank = i + 1 }).ToList();
    }

    private CancellationTokenSource? _watchlistLoadCts;

    private async Task LoadWatchlistAsync()
    {
        _watchlistLoadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _watchlistLoadCts = cts;
        var ct = cts.Token;

        var usingDefaults = _watchlist.Tickers.Count == 0;
        var tickers = ResolveWatchlistTickers(_watchlist.Tickers);

        IsWatchlistBusy = true;
        WatchlistErrorMessage = null;
        try
        {
            var overviews = new StockOverview?[tickers.Count];
            var analysts = new AnalystData?[tickers.Count];
            await Parallel.ForEachAsync(Enumerable.Range(0, tickers.Count), ct, async (i, token) =>
            {
                var overviewTask = _stockAnalysis.GetOverviewAsync(tickers[i], ct: token);
                // Best-effort: a ticker with no analyst coverage (or a transient fetch failure)
                // still gets a row - it just sorts last on the Upside/Rating pills, same degrade
                // pattern as TerminalViewModel.FetchAnalystBestEffortAsync.
                var analystTask = FetchAnalystBestEffortAsync(tickers[i], token);
                await Task.WhenAll(overviewTask, analystTask);
                overviews[i] = overviewTask.Result;
                analysts[i] = analystTask.Result;
            });
            if (ct.IsCancellationRequested) return;

            IsShowingSuggestedTickers = usingDefaults;
            _rawWatchlistRows = Enumerable.Range(0, tickers.Count)
                .Where(i => overviews[i] is not null)
                .Select(i =>
                {
                    var o = overviews[i]!;
                    var analyst = analysts[i];
                    return new WatchlistRow(
                        Rank: 0, // renumbered by SortWatchlistRows below
                        Ticker: o.Ticker, Name: o.Name, Price: o.Price, ChangePercent: o.ChangePercent,
                        QuantScore: o.QuantScore, Signal: o.Signal,
                        UpsidePotentialPct: AnalystAnalyzer.UpsidePotentialPct(analyst?.TargetMean, analyst?.CurrentPrice ?? o.Price),
                        ConsensusRating: analyst?.ConsensusRating,
                        AnalystRatingRank: AnalystAnalyzer.ConsensusRatingRank(analyst?.ConsensusRating));
                })
                .ToList();
            WatchlistRows = SortWatchlistRows(_rawWatchlistRows, WatchlistRankMetric.Metric);

            // Only the user's own curated watchlist, never the suggested-defaults fallback - a
            // "AAPL moved to Buy" briefing about a ticker the user never actually added would just
            // be confusing noise, not a personally relevant signal.
            if (!usingDefaults)
            {
                var changes = _sessionBriefing.RecordAndDiff(
                    _rawWatchlistRows.Select(r => (r.Ticker, r.Name, r.Signal)).ToList());
                if (changes.Count > 0) WeakReferenceMessenger.Default.Send(new WatchlistBriefingMessage(changes));
            }

            if (WatchlistRows.Count == 0) WatchlistErrorMessage = "Couldn't load data for any watched tickers right now.";
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer load - ignore
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested) WatchlistErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            if (!ct.IsCancellationRequested) IsWatchlistBusy = false;
        }
    }

    /// <summary>Best-effort analyst-coverage fetch backing the Upside/Rating watchlist columns -
    /// degrades to null on any failure rather than dropping the row, same shape as
    /// TerminalViewModel.FetchAnalystBestEffortAsync.</summary>
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

    /// <summary>CSV export, same left-to-right column order as the on-screen watchlist table.
    /// Public static (pure, no I/O) so it's directly unit-testable - the actual file-save dialog is a
    /// code-behind concern (UniverseView.axaml.cs), not something a ViewModel should reach for an
    /// Avalonia StorageProvider to do itself.</summary>
    public static string BuildWatchlistCsv(IReadOnlyList<WatchlistRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Rank,Ticker,Name,Price,Change%,Score,Upside%,Rating,Signal");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                r.Rank, CsvField(r.Ticker), CsvField(r.Name), r.Price.ToString("0.00"), r.ChangePercent.ToString("0.00"),
                r.QuantScore.ToString("0.0"), r.UpsidePotentialPct?.ToString("0.0") ?? "", CsvField(r.ConsensusRating ?? ""),
                r.Signal));
        }
        return sb.ToString();
    }

    /// <summary>Same shape as BuildWatchlistCsv but for the Top 20 table (no Change% column - Top20Row
    /// doesn't carry one, matching what's actually shown on screen there).</summary>
    public static string BuildTop20Csv(IReadOnlyList<Top20Row> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Rank,Ticker,Name,Price,Score,Upside%,Rating,Signal");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                r.Rank, CsvField(r.Ticker), CsvField(r.Name), r.Price.ToString("0.00"),
                r.QuantScore.ToString("0.0"), r.UpsidePotentialPct?.ToString("0.0") ?? "", CsvField(r.ConsensusRating ?? ""),
                r.Signal));
        }
        return sb.ToString();
    }

    /// <summary>RFC 4180 quoting: only wraps a field in quotes (doubling any internal quotes) when it
    /// actually contains a comma, quote, or newline - most tickers/ratings never need it, but company
    /// names sometimes do (e.g. "Berkshire Hathaway, Inc.").</summary>
    private static string CsvField(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
}
