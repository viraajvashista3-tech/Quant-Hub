using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
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
    private bool _showBollingerBands;

    [ObservableProperty]
    private IReadOnlyList<NewsItemVm> _headlines = [];

    [ObservableProperty]
    private string? _newsSentimentLabel;

    [ObservableProperty]
    private IReadOnlyList<SignalReason> _reasons = [];

    [ObservableProperty]
    private IReadOnlyList<ScoreBreakdownRow> _scoreBreakdown = [];

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

    public string HeroSentence => Overview?.Signal switch
    {
        Signal.Buy => "Looks like a good time to consider buying.",
        Signal.Avoid => "Signals suggest caution — consider sitting this one out.",
        _ => "Mixed signals — a 'wait and see' situation."
    };

    public TerminalViewModel(AppState appState, StockAnalysisService stockAnalysis, SettingsService settings)
    {
        _appState = appState;
        _stockAnalysis = stockAnalysis;
        _settings = settings;
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
                BuildReasons();
                BuildBreakdown();
            }
        };

        _ = LoadAsync();
    }

    partial void OnSelectedPeriodChanged(PeriodOption value) => _ = LoadAsync();

    partial void OnShowBollingerBandsChanged(bool value) => _ = LoadAsync();

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
            var overviewTask = _stockAnalysis.GetOverviewAsync(ticker, ct);
            var historyTask = _stockAnalysis.GetHistoryAsync(ticker, SelectedPeriod.Tag, ct);
            var newsTask = _stockAnalysis.GetNewsAsync(ticker, ct);
            await Task.WhenAll(overviewTask, historyTask, newsTask);
            if (ct.IsCancellationRequested) return;

            Overview = overviewTask.Result;
            OnPropertyChanged(nameof(HasLimitedData));
            OnPropertyChanged(nameof(HeroEmoji));
            OnPropertyChanged(nameof(HeroSentence));

            if (Overview is null)
            {
                ErrorMessage = $"No data found for {ticker}.";
                PriceSeries.Clear();
                RsiSeries.Clear();
                Headlines = [];
                Reasons = [];
                ScoreBreakdown = [];
                return;
            }

            BuildCharts(historyTask.Result);
            BuildReasons();
            BuildBreakdown();

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

    private void BuildCharts(StockHistory? history)
    {
        PriceSeries.Clear();
        RsiSeries.Clear();
        PriceXAxes.Clear();
        PriceYAxes.Clear();
        RsiXAxes.Clear();
        RsiYAxes.Clear();

        if (history is null || history.Bars.Count == 0) { Legend = []; return; }

        var bars = history.Bars;
        var thinnedLabels = MonthLabels(bars);

        // Genuine null (not double.NaN) - LiveCharts2 renders a NaN-seeded series as invisible
        // entirely once any leading value is NaN, which is exactly the shape of MA50/MA200/RSI
        // (all have a leading gap before their window fills). A nullable array lets LiveCharts2
        // skip the gap and still draw the rest of the line.
        double?[] Column(Func<PriceBar, double?> selector) => bars.Select(selector).ToArray();

        var legend = new List<LegendItem> { new("#00BFFF", "Price"), new("#F59E0B", "MA50"), new("#8B5CF6", "MA200") };

        PriceSeries.Add(LineOf(Column(b => b.Close), "Price", ChartPalette.Primary, 2));
        PriceSeries.Add(LineOf(Column(b => b.Ma50), "MA50", ChartPalette.Warning, 1));
        PriceSeries.Add(LineOf(Column(b => b.Ma200), "MA200", ChartPalette.ChartAccent2, 1));

        if (IsPro && ShowBollingerBands)
        {
            PriceSeries.Add(LineOf(Column(b => b.BbUpper), "BB Upper", ChartPalette.ChartAccent3, 1));
            PriceSeries.Add(LineOf(Column(b => b.BbLower), "BB Lower", ChartPalette.ChartAccent3, 1));
            PriceSeries.Add(LineOf(Column(b => b.BbMa20), "BB Mid", ChartPalette.ChartAccent3, 1));
            legend.Add(new LegendItem("#6366F1", "Bollinger Bands"));
        }

        Legend = legend;

        PriceXAxes.Add(TextAxis(thinnedLabels));
        PriceYAxes.Add(NumericAxis(labeler: v => "$" + v.ToString("0")));

        var rsi = Column(b => b.Rsi);
        RsiSeries.Add(LineOf(Enumerable.Repeat((double?)70.0, bars.Count).ToArray(), "Overbought (70)", ChartPalette.Destructive, 1));
        RsiSeries.Add(LineOf(Enumerable.Repeat((double?)30.0, bars.Count).ToArray(), "Oversold (30)", ChartPalette.Positive, 1));
        RsiSeries.Add(LineOf(rsi, "RSI", SKColor.Parse("#22D3EE"), 2, fill: SKColor.Parse("#2622D3EE")));

        RsiXAxes.Add(TextAxis(thinnedLabels));
        RsiYAxes.Add(NumericAxis(min: 0, max: 100));
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

    private void BuildBreakdown()
    {
        if (Overview is not { } o) { ScoreBreakdown = []; return; }
        ScoreBreakdown =
        [
            new ScoreBreakdownRow("Trend", o.TrendScore ?? 0, 30),
            new ScoreBreakdownRow("Momentum", o.MomentumScore ?? 0, 20),
            new ScoreBreakdownRow("MACD Crossover", o.MacdScore ?? 0, 15),
            new ScoreBreakdownRow("Volume Surge", o.VolScore ?? 0, 10),
            new ScoreBreakdownRow("News Sentiment", o.SentimentContrib ?? 0, 40)
        ];
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

    private static SignalReason SentimentReason(StockOverview o)
    {
        var score = o.SentimentScore;
        var verdict = score > 0.05 ? "Positive" : score < -0.05 ? "Negative" : "Neutral";
        return new SignalReason(verdict, "News sentiment",
            $"Recent headline sentiment scores {score:0.0000} on a -1 to +1 scale.",
            "What people are saying", "Recent news coverage sentiment, summarized.", o.SentimentContrib ?? 0);
    }
}
