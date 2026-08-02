using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using QuantHub.Core.Analysis;
using QuantHub.Core.Backtesting;
using QuantHub.Core.Models;
using QuantHub.Core.Services;
using QuantHub.Desktop.Services;
using QuantHub.Desktop.Theming;
using SkiaSharp;

namespace QuantHub.Desktop.ViewModels.Pages;

public sealed record PeriodOption(string Tag, string Label);

public sealed record NewsItemVm(string Title, string Url, string? PublishedAt, string Source, double? Sentiment);

public sealed record SignalReason(string Verdict, string ProLabel, string ProDetail, string SimpleLabel, string SimpleDetail, double Points);

public sealed record ScoreBreakdownRow(string Label, double Value, double MaxAbs)
{
    public double AbsValue => Math.Abs(Value);
}

public sealed record LegendItem(string Color, string Label);

/// <summary>Main dashboard page - price/RSI charts, key metrics, Quant Score breakdown, the
/// "Why This Signal?" reasoning card, and news headlines. Reacts to both the active ticker and
/// the view mode (beginner vs standard, pro-gated extras) changing.</summary>
public sealed partial class TerminalViewModel : ObservableObject, IRefreshablePage
{
    private readonly AppState _appState;
    private readonly StockAnalysisService _stockAnalysis;
    private readonly SettingsService _settings;
    private readonly ScoreWeightsService _scoreWeights;
    private readonly PredictionLogService _predictionLog;

    public IReadOnlyList<PeriodOption> Periods { get; } =
    [
        new("ytd", "YTD"), new("6mo", "6M"), new("1y", "1Y"), new("2y", "2Y"), new("5y", "5Y")
    ];

    public ObservableCollection<ISeries> PriceSeries { get; } = [];
    public ObservableCollection<Axis> PriceXAxes { get; } = [];
    public ObservableCollection<Axis> PriceYAxes { get; } = [];
    public ObservableCollection<ISeries> RsiSeries { get; } = [];
    public ObservableCollection<Axis> RsiXAxes { get; } = [];
    public ObservableCollection<Axis> RsiYAxes { get; } = [];
    public ObservableCollection<ISeries> CandlestickSeries { get; } = [];
    public ObservableCollection<Axis> CandlestickXAxes { get; } = [];
    public ObservableCollection<Axis> CandlestickYAxes { get; } = [];
    public ObservableCollection<ISeries> BollingerSeries { get; } = [];
    public ObservableCollection<Axis> BollingerXAxes { get; } = [];
    public ObservableCollection<Axis> BollingerYAxes { get; } = [];
    public ObservableCollection<ISeries> ScoreHistorySeries { get; } = [];
    public ObservableCollection<Axis> ScoreHistoryXAxes { get; } = [];
    public ObservableCollection<Axis> ScoreHistoryYAxes { get; } = [];

    [ObservableProperty]
    private IReadOnlyList<LegendItem> _legend = [];

    [ObservableProperty]
    private PeriodOption _selectedPeriod;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private StockOverview? _overview;

    [ObservableProperty]
    private IReadOnlyList<NewsItemVm> _headlines = [];

    [ObservableProperty]
    private string _compareTickerInput = "";

    [ObservableProperty]
    private string? _compareTicker;

    [ObservableProperty]
    private string? _newsSentimentLabel;

    [ObservableProperty]
    private IReadOnlyList<SignalReason> _reasons = [];

    [ObservableProperty]
    private IReadOnlyList<ScoreBreakdownRow> _scoreBreakdown = [];

    [ObservableProperty]
    private bool _hasScoreHistory;

    [ObservableProperty]
    private string? _scoreHistoryExplanation;

    [ObservableProperty]
    private string? _recommendationLine;

    [ObservableProperty]
    private string? _convictionText;

    [ObservableProperty]
    private double _convictionRatio;

    public bool IsPro => _settings.IsPro;
    public bool IsBeginner => _settings.ViewMode == ViewMode.Beginner;
    public bool IsIntermediatePlus => _settings.ViewMode != ViewMode.Beginner;
    public bool HasLimitedData => Overview is { Ma50: null, Ma200: null };

    public string HeroEmoji => Overview?.Signal switch
    {
        Signal.Buy => "📈",
        Signal.Avoid => "📉",
        _ => "⚖️"
    };

    /// <summary>Describes what the indicators show, deliberately not a directive ("consider buying"/
    /// "sit this one out") - this app's own track record (Track Record page) shows these signals
    /// have limited standalone predictive power, so the copy shouldn't imply more confidence than
    /// that track record backs up.</summary>
    public string HeroSentence => Overview?.Signal switch
    {
        Signal.Buy => "Technical indicators lean bullish right now.",
        Signal.Avoid => "Technical indicators lean bearish right now.",
        _ => "Technical indicators are mixed right now."
    };

    public TerminalViewModel(AppState appState, StockAnalysisService stockAnalysis, SettingsService settings,
        ScoreWeightsService scoreWeights, PredictionLogService predictionLog)
    {
        _appState = appState;
        _stockAnalysis = stockAnalysis;
        _settings = settings;
        _scoreWeights = scoreWeights;
        _predictionLog = predictionLog;
        _selectedPeriod = Periods[2];

        _appState.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.ActiveTicker)) _ = LoadAsync();
        };
        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(SettingsService.ViewMode)) return;
            OnPropertyChanged(nameof(IsPro));
            OnPropertyChanged(nameof(IsBeginner));
            OnPropertyChanged(nameof(IsIntermediatePlus));
            if (Overview is not null)
            {
                // Candlestick/Bollinger are now dedicated Pro-only charts (not toggles) built
                // directly from IsPro, so switching tiers must rebuild them immediately rather than
                // waiting for the next ticker/period reload - same reasoning as rebuilding the score
                // breakdown/reasons cards here.
                BuildCharts(_lastHistory);
                BuildReasons();
                BuildBreakdown();
                BuildConviction();
                BuildScoreHistory();
            }
        };
        _scoreWeights.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ScoreWeightsService.Current)) _ = LoadAsync();
        };
        // A prediction is logged fire-and-forget after each load completes (see LoadAsync) - this
        // catches the moment it actually lands (and any later maturity evaluation) so the sparkline
        // picks up today's point without needing a full page reload.
        _predictionLog.Updated += (_, _) => BuildScoreHistory();

        _ = LoadAsync();
    }

    partial void OnSelectedPeriodChanged(PeriodOption value) => _ = LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void SelectPeriod(PeriodOption period) => SelectedPeriod = period;

    [RelayCommand]
    private static void OpenLink(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // best-effort - a malformed or unreachable URL shouldn't crash the app
        }
    }

    private CancellationTokenSource? _loadCts;
    private StockHistory? _lastHistory;
    private StockHistory? _lastCompareHistory;
    private AnalystData? _analystData;

    /// <summary>Backs the "Compare to" AutoCompleteBox's AsyncPopulator (wired in TerminalView.axaml.cs
    /// code-behind, same reasoning as ShellViewModel.SearchTickersAsync).</summary>
    public Task<IReadOnlyList<TickerSearchResult>> SearchTickersAsync(string query, CancellationToken ct) =>
        _stockAnalysis.SearchTickersAsync(query, ct);

    [RelayCommand]
    private async Task SetCompareTickerAsync(string symbol)
    {
        var upper = symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(upper) || upper == Overview?.Ticker) return; // comparing a ticker to itself is meaningless

        CompareTicker = upper;
        CompareTickerInput = "";
        _lastCompareHistory = await _stockAnalysis.GetHistoryAsync(upper, SelectedPeriod.Tag);
        BuildCharts(_lastHistory);
    }

    [RelayCommand]
    private void ClearCompareTicker()
    {
        CompareTicker = null;
        _lastCompareHistory = null;
        BuildCharts(_lastHistory);
    }

    /// <summary>Cancels any in-flight load before starting a new one, so a slower request for a
    /// ticker the user has already moved on from can never overwrite the UI with stale results.</summary>
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
            var overviewTask = _stockAnalysis.GetOverviewAsync(ticker, _scoreWeights.Current, ct);
            var historyTask = _stockAnalysis.GetHistoryAsync(ticker, SelectedPeriod.Tag, ct);
            var newsTask = _stockAnalysis.GetNewsAsync(ticker, ct);
            // Keeps the comparison series in sync when the period changes (a compare ticker doesn't
            // depend on the main ticker's identity, only on which period is selected) - re-fetched
            // alongside the main load rather than only when SetCompareTickerAsync itself runs.
            var compareHistoryTask = CompareTicker is { } compareTicker
                ? _stockAnalysis.GetHistoryAsync(compareTicker, SelectedPeriod.Tag, ct)
                : Task.FromResult<StockHistory?>(null);
            // Best-effort: analyst coverage backs the recommendation line but must never fail the
            // whole page load if Yahoo's analyst endpoint hiccups or a ticker simply has no coverage.
            var analystTask = FetchAnalystBestEffortAsync(ticker, ct);
            await Task.WhenAll(overviewTask, historyTask, newsTask, compareHistoryTask, analystTask);
            if (ct.IsCancellationRequested) return;

            Overview = overviewTask.Result;
            OnPropertyChanged(nameof(HasLimitedData));
            OnPropertyChanged(nameof(HeroEmoji));
            OnPropertyChanged(nameof(HeroSentence));

            if (Overview is null)
            {
                ErrorMessage = $"No data found for {ticker}.";
                _lastHistory = null;
                _analystData = null;
                PriceSeries.Clear();
                RsiSeries.Clear();
                CandlestickSeries.Clear();
                BollingerSeries.Clear();
                Headlines = [];
                Reasons = [];
                ScoreBreakdown = [];
                HasScoreHistory = false;
                ConvictionText = null;
                RecommendationLine = null;
                return;
            }

            _lastHistory = historyTask.Result;
            _lastCompareHistory = compareHistoryTask.Result;
            _analystData = analystTask.Result;
            BuildCharts(_lastHistory);
            BuildReasons();
            BuildBreakdown();
            BuildConviction();
            BuildScoreHistory();
            RecommendationLine = BuildRecommendationLine(Overview, _analystData);
            _predictionLog.LogInBackground(Overview);

            var maxHeadlines = _settings.ViewMode switch { ViewMode.Pro => 8, ViewMode.Intermediate => 6, _ => 4 };
            var news = newsTask.Result;
            NewsSentimentLabel = news.SentimentLabel;
            Headlines = news.Headlines.Take(maxHeadlines)
                .Select(h => new NewsItemVm(h.Title, h.Url, h.PublishedAt, SourceFromUrl(h.Url), h.Sentiment))
                .ToList();
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

    /// <summary>Best-effort analyst-coverage fetch for the recommendation line - degrades to null on
    /// any failure (network hiccup, or a ticker with genuinely no analyst coverage) rather than
    /// failing the whole page load, same shape as BacktestEngine.FetchInsiderPurchaseDatesAsync.</summary>
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

    /// <summary>A single concrete, numbers-included recommendation sentence - unlike HeroSentence
    /// (a generic, ticker-agnostic template), this always names the actual score and, when analyst
    /// coverage is available, the actual consensus rating and computed upside/downside. Public static
    /// so it's directly unit-testable without a live network call.</summary>
    public static string BuildRecommendationLine(StockOverview o, AnalystData? analyst)
    {
        var signalWord = o.Signal switch { Signal.Buy => "Buy", Signal.Avoid => "Avoid", _ => "Hold" };
        var scorePart = $"{o.Ticker} is a {signalWord} (Quant Score {o.QuantScore:0})";

        if (analyst is null || analyst.ConsensusRating == "N/A")
            return $"{scorePart}. Analyst coverage isn't available for this ticker right now.";

        var upside = AnalystAnalyzer.UpsidePotentialPct(analyst.TargetMean, analyst.CurrentPrice ?? o.Price);
        if (upside is null)
            return $"{scorePart}. Wall Street consensus is {analyst.ConsensusRating}, but no price target is available.";

        var direction = upside >= 0 ? "upside" : "downside";
        return $"{scorePart}. Wall Street consensus is {analyst.ConsensusRating}, implying {Math.Abs(upside.Value):0.0}% {direction} to the average ${analyst.TargetMean:0.00} target.";
    }

    private static string SourceFromUrl(string url)
    {
        try
        {
            return new Uri(url).Host.Replace("www.", "");
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Builds the always-shown price/MA + RSI charts, plus (Pro only) two dedicated
    /// candlestick and Bollinger Bands charts below them - previously these were toggles that
    /// replaced/overlaid the main chart; now they're their own permanent panels so a Pro user sees
    /// all three views at once instead of switching between them.</summary>
    private void BuildCharts(StockHistory? history)
    {
        PriceSeries.Clear();
        RsiSeries.Clear();
        CandlestickSeries.Clear();
        BollingerSeries.Clear();
        PriceXAxes.Clear();
        PriceYAxes.Clear();
        RsiXAxes.Clear();
        RsiYAxes.Clear();
        CandlestickXAxes.Clear();
        CandlestickYAxes.Clear();
        BollingerXAxes.Clear();
        BollingerYAxes.Clear();

        if (history is null || history.Bars.Count == 0) { Legend = []; return; }

        var bars = history.Bars;
        var thinnedLabels = MonthLabels(bars);

        // Genuine null (not double.NaN) - LiveCharts2 renders a NaN-seeded series as invisible
        // entirely once any leading value is NaN, which is exactly the shape of MA50/MA200/RSI
        // (all have a leading gap before their window fills). A nullable array lets LiveCharts2
        // skip the gap and still draw the rest of the line.
        double?[] Column(Func<PriceBar, double?> selector) => bars.Select(selector).ToArray();

        if (CompareTicker is { } compareTicker && _lastCompareHistory is { Bars.Count: > 0 })
        {
            BuildComparisonPriceChart(bars, _lastCompareHistory.Bars, compareTicker, thinnedLabels);
        }
        else
        {
            BuildMainPriceChart(bars, Column, thinnedLabels);
        }

        // RSI, Candlestick, and Bollinger Bands charts below always describe the main ticker only -
        // comparison mode only replaces the main price chart, not these.
        BuildRsiChart(bars, Column, thinnedLabels);
        if (!IsPro) return;
        BuildCandlestickChart(bars, Column, thinnedLabels);
        BuildBollingerChart(bars, Column, thinnedLabels);
    }

    private void BuildMainPriceChart(IReadOnlyList<PriceBar> bars, Func<Func<PriceBar, double?>, double?[]> column, string[] labels)
    {
        PriceSeries.Add(LineOf(column(b => b.Close), "Price", ChartPalette.Primary, 2));
        PriceSeries.Add(LineOf(column(b => b.Ma50), "MA50", ChartPalette.Warning, 1));
        PriceSeries.Add(LineOf(column(b => b.Ma200), "MA200", ChartPalette.ChartAccent2, 1));
        Legend =
        [
            new LegendItem("#00BFFF", "Price"),
            new LegendItem("#F59E0B", "MA50"),
            new LegendItem("#8B5CF6", "MA200")
        ];

        PriceXAxes.Add(TextAxis(labels));
        PriceYAxes.Add(NumericAxis(labeler: PriceAxisLabel));
    }

    /// <summary>Rebases both series to % change from their own first bar (instead of raw $) since
    /// overlaying two different-priced stocks' raw prices is meaningless. The compare series is
    /// aligned to the main series' dates via a date-keyed lookup rather than bar index, since the two
    /// tickers can have different bar counts (holidays, listing dates) - same alignment technique
    /// RelativeStrengthSignal/BacktestEngine already use for cross-ticker comparisons.</summary>
    private void BuildComparisonPriceChart(IReadOnlyList<PriceBar> bars, IReadOnlyList<PriceBar> compareBars, string compareTicker, string[] labels)
    {
        var baseClose = bars[0].Close;
        var mainPct = bars.Select(b => baseClose != 0 ? (double?)((b.Close - baseClose) / baseClose * 100) : null).ToArray();

        var compareByDate = compareBars.ToDictionary(b => b.Date, b => b.Close);
        var compareBase = compareBars[0].Close;
        var comparePct = bars.Select(b =>
            compareBase != 0 && compareByDate.TryGetValue(b.Date, out var c)
                ? (double?)((c - compareBase) / compareBase * 100)
                : null).ToArray();

        var mainTicker = Overview?.Ticker ?? "This stock";
        PriceSeries.Add(LineOf(mainPct, mainTicker, ChartPalette.Primary, 2));
        PriceSeries.Add(LineOf(comparePct, compareTicker, ChartPalette.ChartAccent2, 2));
        Legend =
        [
            new LegendItem("#00BFFF", mainTicker),
            new LegendItem("#8B5CF6", compareTicker)
        ];

        PriceXAxes.Add(TextAxis(labels));
        PriceYAxes.Add(NumericAxis(labeler: v => $"{v:+0;-0}%"));
    }

    private void BuildRsiChart(IReadOnlyList<PriceBar> bars, Func<Func<PriceBar, double?>, double?[]> column, string[] labels)
    {
        var rsi = column(b => b.Rsi);
        RsiSeries.Add(LineOf(Enumerable.Repeat((double?)70.0, bars.Count).ToArray(), "Overbought (70)", ChartPalette.Destructive, 1));
        RsiSeries.Add(LineOf(Enumerable.Repeat((double?)30.0, bars.Count).ToArray(), "Oversold (30)", ChartPalette.Positive, 1));
        RsiSeries.Add(LineOf(rsi, "RSI", SKColor.Parse("#22D3EE"), 2, fill: SKColor.Parse("#2622D3EE")));

        RsiXAxes.Add(TextAxis(labels));
        RsiYAxes.Add(NumericAxis(min: 0, max: 100));
    }

    /// <summary>Pro-only: candlestick view (with the same MA50/MA200 overlay for trend context).</summary>
    private void BuildCandlestickChart(IReadOnlyList<PriceBar> bars, Func<Func<PriceBar, double?>, double?[]> column, string[] labels)
    {
        var candles = bars.Select(b => new FinancialPointI(high: b.High, open: b.Open, close: b.Close, low: b.Low)).ToArray();
        CandlestickSeries.Add(new CandlesticksSeries<FinancialPointI>
        {
            Values = candles,
            UpFill = new SolidColorPaint(ChartPalette.Positive),
            UpStroke = new SolidColorPaint(ChartPalette.Positive),
            DownFill = new SolidColorPaint(ChartPalette.Destructive),
            DownStroke = new SolidColorPaint(ChartPalette.Destructive)
        });
        CandlestickSeries.Add(LineOf(column(b => b.Ma50), "MA50", ChartPalette.Warning, 1));
        CandlestickSeries.Add(LineOf(column(b => b.Ma200), "MA200", ChartPalette.ChartAccent2, 1));
        CandlestickXAxes.Add(TextAxis(labels));
        CandlestickYAxes.Add(NumericAxis(labeler: PriceAxisLabel));
    }

    /// <summary>Pro-only: Bollinger Bands view (price + upper/lower/mid band, its own focused chart
    /// rather than overlaid on the main one).</summary>
    private void BuildBollingerChart(IReadOnlyList<PriceBar> bars, Func<Func<PriceBar, double?>, double?[]> column, string[] labels)
    {
        BollingerSeries.Add(LineOf(column(b => b.Close), "Price", ChartPalette.Primary, 2));
        BollingerSeries.Add(LineOf(column(b => b.BbUpper), "BB Upper", ChartPalette.ChartAccent3, 1));
        BollingerSeries.Add(LineOf(column(b => b.BbLower), "BB Lower", ChartPalette.ChartAccent3, 1));
        BollingerSeries.Add(LineOf(column(b => b.BbMa20), "BB Mid", ChartPalette.ChartAccent3, 1));
        BollingerXAxes.Add(TextAxis(labels));
        BollingerYAxes.Add(NumericAxis(labeler: PriceAxisLabel));
    }

    /// <summary>One label per calendar month (e.g. "Aug 25"), placed at that month's first bar -
    /// reads like a normal financial chart axis instead of exact-but-meaningless daily dates.</summary>
    private static string[] MonthLabels(IReadOnlyList<PriceBar> bars)
    {
        var labels = new string[bars.Count];
        Array.Fill(labels, "");
        string? lastMonth = null;
        for (var i = 0; i < bars.Count; i++)
        {
            if (!DateTime.TryParse(bars[i].Date, out var date)) continue;
            var monthKey = date.ToString("yyyy-MM");
            if (monthKey == lastMonth) continue;
            lastMonth = monthKey;
            labels[i] = date.ToString("MMM yy");
        }
        return labels;
    }

    private static Axis TextAxis(string[] labels) => new()
    {
        Labels = labels,
        LabelsPaint = new SolidColorPaint(ChartPalette.AxisText),
        SeparatorsPaint = new SolidColorPaint(ChartPalette.AxisLine) { StrokeThickness = 1 },
        TextSize = 11
    };

    /// <summary>Scales decimal precision to price magnitude so sub-$10 names (e.g. SGX blue chips,
    /// US penny stocks) don't have every gridline round to the same whole dollar - "0" precision
    /// made a stock trading the whole year between $0.80-$1.46 (VC2.SI) show "$1" on every single
    /// gridline, looking like the chart was broken.</summary>
    private static string PriceAxisLabel(double v) => v < 10 ? $"${v:0.00}" : v < 100 ? $"${v:0.0}" : $"${v:0}";

    private static Axis NumericAxis(double? min = null, double? max = null, Func<double, string>? labeler = null)
    {
        var axis = new Axis
        {
            LabelsPaint = new SolidColorPaint(ChartPalette.AxisText),
            SeparatorsPaint = new SolidColorPaint(ChartPalette.AxisLine) { StrokeThickness = 1 },
            TextSize = 11
        };
        if (min is { } mn) axis.MinLimit = mn;
        if (max is { } mx) axis.MaxLimit = mx;
        if (labeler is not null) axis.Labeler = labeler;
        return axis;
    }

    private static LineSeries<double?> LineOf(double?[] values, string name, SKColor color, double thickness, SKColor? fill = null) => new()
    {
        Values = values,
        Name = name,
        Stroke = new SolidColorPaint(color, (float)thickness),
        Fill = fill is { } f ? new SolidColorPaint(f) : null,
        GeometryStroke = null,
        GeometryFill = null,
        LineSmoothness = 0
    };

    /// <summary>MaxAbs per row reflects whatever weights are actually currently applied (via
    /// ScoreWeightsService recalibration and/or SectorSentimentWeights), not the original hand-picked
    /// 30/20/15/10/40 point values - otherwise the progress bars would clip or under-fill once a
    /// non-default weight is active.</summary>
    private void BuildBreakdown()
    {
        if (Overview is not { } o) { ScoreBreakdown = []; return; }
        var w = _scoreWeights.Current;
        var sentimentWeight = o.SentimentWeightMultiplier ?? 1.0;
        var sentimentLabel = Math.Abs(sentimentWeight - 1.0) > 0.01
            ? $"News Sentiment ({sentimentWeight:0.0}x — {o.Sector})"
            : "News Sentiment";

        ScoreBreakdown =
        [
            new ScoreBreakdownRow("Trend", o.TrendScore ?? 0, QuantScoreCalculator.TrendMax * w.Trend),
            new ScoreBreakdownRow("Momentum", o.MomentumScore ?? 0, QuantScoreCalculator.MomentumMax * w.Momentum),
            new ScoreBreakdownRow("MACD Crossover", o.MacdScore ?? 0, QuantScoreCalculator.MacdMax * w.Macd),
            new ScoreBreakdownRow("Volume Surge", o.VolScore ?? 0, QuantScoreCalculator.VolMax * w.Vol),
            new ScoreBreakdownRow("Mean Reversion (Bollinger)", o.MeanReversionScore ?? 0, QuantScoreCalculator.MeanReversionMax * w.MeanReversion),
            new ScoreBreakdownRow("Short-Term Reversal (1M)", o.PriceMomentumScore ?? 0, QuantScoreCalculator.PriceMomentumMax * w.PriceMomentum),
            new ScoreBreakdownRow(sentimentLabel, o.SentimentContrib ?? 0, 40 * sentimentWeight)
        ];
    }

    /// <summary>How many of the six technical component scores agree in sign with the overall
    /// QuantScore - a cheap, purely-derived "conviction" read using data already on StockOverview
    /// (no new fetch). Deliberately excludes Sentiment (a separate, already-visible contribution) so
    /// this reads as "do the technicals agree with each other", not "does everything sum to the
    /// score" (which would be true by construction).</summary>
    private void BuildConviction()
    {
        if (Overview is not { } o) { ConvictionText = null; ConvictionRatio = 0; return; }

        var components = new[]
        {
            o.TrendScore, o.MomentumScore, o.MacdScore, o.VolScore, o.MeanReversionScore, o.PriceMomentumScore
        }.Where(v => v is not null).Select(v => v!.Value).Where(v => v != 0).ToList();

        if (components.Count == 0) { ConvictionText = null; ConvictionRatio = 0; return; }

        var overallPositive = o.QuantScore >= 0;
        var agreeing = components.Count(v => (v >= 0) == overallPositive);
        ConvictionRatio = agreeing / (double)components.Count;

        ConvictionText = IsBeginner
            ? ConvictionRatio switch
            {
                >= 0.7 => "Most signals agree with this call.",
                <= 0.3 => "Signals are mixed on this call - worth a closer look.",
                _ => "Signals are somewhat mixed on this call."
            }
            : $"{agreeing} of {components.Count} technical signals agree with this call";
    }

    /// <summary>Plots this ticker's own logged QuantScore over time (PredictionLogService entries -
    /// one per day it's been viewed), so the score's trustworthiness can be judged from its own
    /// track record rather than taken on faith. Deliberately does not surface weights, recalibration,
    /// or backtest mechanics anywhere here - this is "how has this ticker's score moved", not a
    /// window into the (intentionally hidden) auto-recalibration pipeline.</summary>
    private void BuildScoreHistory()
    {
        ScoreHistorySeries.Clear();
        ScoreHistoryXAxes.Clear();
        ScoreHistoryYAxes.Clear();

        if (Overview is not { } o)
        {
            HasScoreHistory = false;
            ScoreHistoryExplanation = null;
            return;
        }

        var points = _predictionLog.Entries
            .Where(e => e.Ticker == o.Ticker)
            .OrderBy(e => e.LoggedAtUtc)
            .ToList();

        if (points.Count < 2 || IsBeginner)
        {
            HasScoreHistory = false;
            ScoreHistoryExplanation = null;
            return;
        }

        HasScoreHistory = true;
        var values = points.Select(p => (double?)p.Score).ToArray();
        var labels = points.Select(p => p.LoggedAtUtc.ToString("MMM d")).ToArray();

        ScoreHistorySeries.Add(LineOf(values, "Quant Score", ChartPalette.Primary, 2));
        ScoreHistoryXAxes.Add(TextAxis(labels));
        ScoreHistoryYAxes.Add(NumericAxis());

        ScoreHistoryExplanation = FormatScoreChangeExplanation(PredictionLog.ExplainScoreChange(points));
    }

    /// <summary>Turns PredictionLog.ExplainScoreChange's structured result into the sentence shown
    /// under the sparkline - null when there isn't yet enough breakdown history to explain anything
    /// (distinct from "the score hasn't moved", which does get a sentence).</summary>
    private static string? FormatScoreChangeExplanation(ScoreChangeExplanation? e)
    {
        if (e is null) return null;

        var since = e.SinceUtc.ToString("MMM d");
        if (Math.Abs(e.TotalDelta) < 0.5) return $"The score has stayed roughly flat since {since}.";

        var direction = e.TotalDelta > 0 ? "risen" : "fallen";
        var driverText = e.TopDrivers.Count > 0
            ? " — mostly driven by " + string.Join(" and ", e.TopDrivers.Select(d => $"{d.Label} ({d.Delta:+0.0;-0.0})"))
            : "";
        return $"Since {since}, the score has {direction} by {Math.Abs(e.TotalDelta):0.0} points{driverText}.";
    }

    private void BuildReasons()
    {
        if (Overview is not { } o) { Reasons = []; return; }
        var list = new List<SignalReason>
        {
            Ma200Reason(o),
            Ma50Reason(o),
            CrossReason(o),
            RsiReason(o),
            MacdReason(o),
            VolumeReason(o),
            MeanReversionReason(o),
            PriceMomentumReason(o),
            SentimentReason(o)
        };
        Reasons = list;
    }

    private static SignalReason Ma200Reason(StockOverview o) => o.AboveMa200 switch
    {
        true => new SignalReason("Positive", "Above 200-day MA",
            $"Price ({o.Price:0.00}) is above the 200-day moving average ({o.Ma200:0.00}), a long-term bullish signal.",
            "Long-term trend: bullish", "The stock is trading above its long-term average price.", 15),
        false => new SignalReason("Negative", "Below 200-day MA",
            $"Price ({o.Price:0.00}) is below the 200-day moving average ({o.Ma200:0.00}), a long-term bearish signal.",
            "Long-term trend: bearish", "The stock is trading below its long-term average price.", -15),
        _ => new SignalReason("Neutral", "200-day MA unavailable",
            "Not enough price history to compute a 200-day moving average.",
            "Not enough history", "This stock may be too new or thinly traded for a long-term read.", 0)
    };

    private static SignalReason Ma50Reason(StockOverview o) => o.AboveMa50 switch
    {
        true => new SignalReason("Positive", "Above 50-day MA",
            $"Price ({o.Price:0.00}) is above the 50-day moving average ({o.Ma50:0.00}), a medium-term bullish signal.",
            "Medium-term trend: bullish", "The stock is trading above its recent average price.", 10),
        false => new SignalReason("Negative", "Below 50-day MA",
            $"Price ({o.Price:0.00}) is below the 50-day moving average ({o.Ma50:0.00}), a medium-term bearish signal.",
            "Medium-term trend: bearish", "The stock is trading below its recent average price.", -10),
        _ => new SignalReason("Neutral", "50-day MA unavailable",
            "Not enough price history to compute a 50-day moving average.",
            "Not enough history", "Not enough recent history for a medium-term read.", 0)
    };

    private static SignalReason CrossReason(StockOverview o) => o.GoldenCross switch
    {
        true => new SignalReason("Positive", "Golden Cross",
            "The 50-day moving average is above the 200-day moving average - a classic bullish setup.",
            "Trend momentum: building", "Recent prices are outpacing the long-term average.", 5),
        false => new SignalReason("Negative", "Death Cross",
            "The 50-day moving average is below the 200-day moving average - a classic bearish setup.",
            "Trend momentum: fading", "Recent prices are lagging the long-term average.", -5),
        _ => new SignalReason("Neutral", "Cross unavailable",
            "Not enough history to compare the 50-day and 200-day moving averages.",
            "Not enough history", "Not enough history to judge trend momentum.", 0)
    };

    private static SignalReason RsiReason(StockOverview o)
    {
        var rsi = o.Rsi;
        return rsi switch
        {
            >= 70 => new SignalReason("Warning", "Overbought (RSI ≥ 70)",
                $"RSI is {rsi:0.0}, in overbought territory - a near-term pullback is possible.",
                "Momentum: overheated", "The stock has risen quickly and may be due for a breather.", o.MomentumScore ?? -10),
            >= 60 => new SignalReason("Positive", "Strong bullish momentum (RSI 60-70)",
                $"RSI is {rsi:0.0}, the strongest bullish zone without being overbought.",
                "Momentum: strong", "Buying interest is strong right now.", o.MomentumScore ?? 20),
            >= 50 => new SignalReason("Positive", "Mild bullish momentum (RSI 50-60)",
                $"RSI is {rsi:0.0}, mildly bullish.",
                "Momentum: mildly positive", "Slightly more buyers than sellers lately.", o.MomentumScore ?? 10),
            >= 40 => new SignalReason("Warning", "Neutral / mildly bearish (RSI 40-50)",
                $"RSI is {rsi:0.0}, roughly neutral with a mild bearish lean.",
                "Momentum: flat", "No strong buying or selling pressure either way.", o.MomentumScore ?? -5),
            >= 30 => new SignalReason("Negative", "Weak momentum (RSI 30-40)",
                $"RSI is {rsi:0.0}, in weak/bearish territory.",
                "Momentum: weak", "More sellers than buyers lately.", o.MomentumScore ?? -15),
            _ => new SignalReason("Negative", "Deeply oversold (RSI < 30)",
                $"RSI is {rsi:0.0}, deeply oversold.",
                "Momentum: very weak", "The stock has fallen sharply and selling pressure is heavy.", o.MomentumScore ?? -20)
        };
    }

    private static SignalReason MacdReason(StockOverview o) => o.Macd > o.MacdSignal
        ? new SignalReason("Positive", "MACD bullish crossover",
            $"MACD ({o.Macd:0.0000}) is above its signal line ({o.MacdSignal:0.0000}), indicating upward momentum.",
            "Trend signal: bullish", "A momentum indicator is pointing upward.", 15)
        : new SignalReason("Negative", "MACD bearish crossover",
            $"MACD ({o.Macd:0.0000}) is below its signal line ({o.MacdSignal:0.0000}), indicating downward momentum.",
            "Trend signal: bearish", "A momentum indicator is pointing downward.", -15);

    private static SignalReason VolumeReason(StockOverview o)
    {
        var ratio = o.VolRatio ?? 1.0;
        return ratio switch
        {
            >= 1.5 => new SignalReason("Positive", "Volume surge",
                $"Volume is {ratio:0.00}x the 20-day average - a surge that often confirms a move.",
                "Trading activity: high", "A lot more shares than usual are trading hands.", o.VolScore ?? 10),
            >= 1.0 => new SignalReason("Neutral", "Normal volume",
                $"Volume is {ratio:0.00}x the 20-day average - about normal.",
                "Trading activity: normal", "Trading activity is about what you'd expect.", o.VolScore ?? 5),
            _ => new SignalReason("Neutral", "Below-average volume",
                $"Volume is {ratio:0.00}x the 20-day average - quieter than usual.",
                "Trading activity: low", "Fewer shares than usual are trading hands.", o.VolScore ?? 0)
        };
    }

    private static SignalReason MeanReversionReason(StockOverview o)
    {
        var pctB = o.BollingerPctB;
        return pctB switch
        {
            <= 0.1 => new SignalReason("Positive", "Near lower Bollinger Band",
                $"Price sits near the lower Bollinger Band ({pctB:0.00} on a 0-1 scale), a classic oversold/mean-reversion setup.",
                "Price stretch: oversold", "The price has pulled back further than usual and may be due to bounce back.", o.MeanReversionScore ?? 0),
            >= 0.9 => new SignalReason("Warning", "Near upper Bollinger Band",
                $"Price sits near the upper Bollinger Band ({pctB:0.00} on a 0-1 scale), a classic overbought/mean-reversion setup.",
                "Price stretch: overbought", "The price has run up further than usual and may be due to cool off.", o.MeanReversionScore ?? 0),
            { } p => new SignalReason("Neutral", "Within normal trading range",
                $"Price sits mid-band ({p:0.00} on a 0-1 scale) - not stretched in either direction.",
                "Price stretch: normal", "The price isn't unusually stretched in either direction right now.", o.MeanReversionScore ?? 0),
            null => new SignalReason("Neutral", "Bollinger Bands unavailable",
                "Not enough price history to compute Bollinger Bands.",
                "Not enough history", "Not enough recent history for this read.", 0)
        };
    }

    /// <summary>Framed as reversal, not momentum - backtesting this exact 1-month rate-of-change
    /// showed a consistent, strengthening-with-horizon *negative* correlation with forward returns
    /// (see QuantScoreCalculator.PriceMomentumSignal), the well-documented short-term reversal effect
    /// rather than the momentum continuation a naive reading of "stock is up, that's bullish" would
    /// assume.</summary>
    private static SignalReason PriceMomentumReason(StockOverview o)
    {
        var roc = o.PriceRoc21Pct;
        return roc switch
        {
            > 5 => new SignalReason("Warning", "Stretched after a strong 1-month run-up",
                $"The stock is up {roc:0.0}% over the last month. Historically, stocks that rise this much this fast tend to give back some of the gain shortly after (short-term reversal), rather than keep climbing.",
                "Recent trend: stretched", "The stock has climbed a lot very recently, which has historically tended to cool off rather than continue.", o.PriceMomentumScore ?? 0),
            < -5 => new SignalReason("Positive", "Potential bounce after a sharp 1-month drop",
                $"The stock is down {roc:0.0}% over the last month. Historically, stocks that fall this much this fast have tended to partially bounce back shortly after.",
                "Recent trend: oversold", "The stock has dropped a lot very recently, which has historically tended to bounce back somewhat.", o.PriceMomentumScore ?? 0),
            { } r => new SignalReason("Neutral", "Flat 1-month move",
                $"The stock is roughly flat ({r:+0.0;-0.0}%) over the last month - no strong reversal signal either way.",
                "Recent trend: flat", "The stock hasn't moved much either way over the last month.", o.PriceMomentumScore ?? 0),
            null => new SignalReason("Neutral", "Not enough history",
                "Not enough price history to compute 1-month rate of change.",
                "Not enough history", "This stock may be too new for this read.", 0)
        };
    }

    private static SignalReason SentimentReason(StockOverview o)
    {
        var score = o.SentimentScore;
        var verdict = score > 0.05 ? "Positive" : score < -0.05 ? "Negative" : "Neutral";
        return new SignalReason(verdict, "News sentiment",
            $"Recent headline sentiment scores {score:0.0000} on a -1 to +1 scale.",
            "What people are saying", "Recent news coverage sentiment, summarized.", o.SentimentContrib ?? 0);
    }
}
