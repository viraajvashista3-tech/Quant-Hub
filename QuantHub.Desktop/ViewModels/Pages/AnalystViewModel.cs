using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using QuantHub.Core.Analysis;
using QuantHub.Core.Models;
using QuantHub.Core.Services;
using QuantHub.Desktop.Services;
using QuantHub.Desktop.Theming;
using SkiaSharp;

namespace QuantHub.Desktop.ViewModels.Pages;

/// <summary>Analyst coverage page - consensus rating, price targets, a stacked recommendation-trend
/// chart, and real recorded upgrade/downgrade actions read out as narrative sentences (there is no
/// "quote" text in Yahoo's data - this is the genuine recorded firm/grade/date history, just written
/// out in plain sentences instead of a bare table). Reacts to the active ticker and view mode.</summary>
public sealed partial class AnalystViewModel : ObservableObject, IRefreshablePage
{
    private readonly AppState _appState;
    private readonly StockAnalysisService _stockAnalysis;
    private readonly SettingsService _settings;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private AnalystData? _data;

    public ObservableCollection<ISeries> TrendSeries { get; } = [];
    public ObservableCollection<Axis> TrendXAxes { get; } = [];
    public ObservableCollection<Axis> TrendYAxes { get; } = [];

    public bool IsPro => _settings.IsPro;
    public bool IsBeginner => _settings.ViewMode == ViewMode.Beginner;
    public bool IsIntermediatePlus => !IsBeginner;

    public IBrush ConsensusBrush => Data?.ConsensusRating switch
    {
        "Strong Buy" or "Buy" => ThemeResources.GetBrush("PositiveBrush"),
        "Hold" => ThemeResources.GetBrush("WarningBrush"),
        "Sell" or "Strong Sell" => ThemeResources.GetBrush("DestructiveBrush"),
        _ => ThemeResources.GetBrush("MutedTextBrush")
    };

    /// <summary>Plain-English framing for Beginner: consensus + price target expressed as upside/
    /// downside from the current price instead of raw dollar figures.</summary>
    public string? BeginnerSummary
    {
        get
        {
            if (Data is not { } d) return null;
            var coverage = d.NumAnalysts is { } n and > 0
                ? $"{n} Wall Street analysts cover this stock, and the average view is '{d.ConsensusRating}'."
                : $"The average analyst view is '{d.ConsensusRating}'.";

            if (AnalystAnalyzer.UpsidePotentialPct(d.TargetMean, d.CurrentPrice) is not { } pct)
            {
                return coverage;
            }

            var direction = pct >= 0 ? "upside" : "downside";
            return $"{coverage} The average price target of {d.TargetMean:0.00} implies about {Math.Abs(pct):0.0}% {direction} from the current price of {d.CurrentPrice:0.00}.";
        }
    }

    /// <summary>Where the current price and the average estimate each sit within the analyst
    /// Low-High target range, as a 0-100 position - a distinct visual read (a gauge) on the same
    /// numbers the PRICE TARGETS card already shows as raw figures. Null (gauge hidden) if the range
    /// is degenerate (High <= Low) or any input is missing.</summary>
    public bool HasTargetGauge => Data is { TargetLow: { } lo, TargetHigh: { } hi, CurrentPrice: not null } && hi > lo;

    public double TargetGaugeMin => Data?.TargetLow ?? 0;
    public double TargetGaugeMax => Data?.TargetHigh ?? 0;
    public double TargetGaugeValue => Data?.CurrentPrice ?? 0;

    public string? TargetGaugeLabel
    {
        get
        {
            if (!HasTargetGauge || Data is not { } d) return null;
            var lo = d.TargetLow!.Value;
            var hi = d.TargetHigh!.Value;
            var range = hi - lo;
            var curPct = Math.Clamp((d.CurrentPrice!.Value - lo) / range * 100, 0, 100);
            var meanText = d.TargetMean is { } mean
                ? $" the average estimate sits at {Math.Clamp((mean - lo) / range * 100, 0, 100):0}%."
                : "";
            return $"Current price is {curPct:0}% of the way from the low to the high estimate;{meanText}";
        }
    }

    public IReadOnlyList<string> NarrativeActions
    {
        get
        {
            if (Data?.RecentActions is not { } actions || Data.Ticker is not { } ticker) return [];
            var count = IsBeginner ? 5 : IsPro ? 40 : 15;
            return actions.Take(count).Select(a => BuildActionSentence(a, ticker)).ToList();
        }
    }

    public AnalystViewModel(AppState appState, StockAnalysisService stockAnalysis, SettingsService settings)
    {
        _appState = appState;
        _stockAnalysis = stockAnalysis;
        _settings = settings;

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
            OnPropertyChanged(nameof(NarrativeActions));
        };

        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

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
            var result = await _stockAnalysis.GetAnalystAsync(ticker, ct);
            if (ct.IsCancellationRequested) return;

            Data = result;
            OnPropertyChanged(nameof(ConsensusBrush));
            OnPropertyChanged(nameof(BeginnerSummary));
            OnPropertyChanged(nameof(NarrativeActions));
            OnPropertyChanged(nameof(HasTargetGauge));
            OnPropertyChanged(nameof(TargetGaugeMin));
            OnPropertyChanged(nameof(TargetGaugeMax));
            OnPropertyChanged(nameof(TargetGaugeValue));
            OnPropertyChanged(nameof(TargetGaugeLabel));

            if (Data is null)
            {
                ErrorMessage = $"No analyst coverage found for {ticker}.";
                TrendSeries.Clear();
                return;
            }

            BuildTrendChart(Data.RecommendationTrend);
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

    /// <summary>Turns one real recorded upgrade/downgrade/initiation/reiteration into a plain
    /// sentence. Yahoo's action codes are short forms ("up"/"down"/"main"/"init"/"reit") mixed with
    /// occasional full words depending on the ticker, so both are handled; anything unrecognized
    /// falls back to the raw action text rather than guessing.</summary>
    private static string BuildActionSentence(AnalystAction a, string ticker)
    {
        var dateStr = a.Date is { } d && DateTime.TryParse(d, out var parsed) ? $" on {parsed:MMM d, yyyy}" : "";
        var hasGradeChange = a.FromGrade is { } from && a.ToGrade is { } to && !string.Equals(from, to, StringComparison.OrdinalIgnoreCase);

        return a.Action.ToLowerInvariant() switch
        {
            "up" or "upgrade" or "upgraded" => hasGradeChange
                ? $"{a.Firm} upgraded {ticker} from {a.FromGrade} to {a.ToGrade}{dateStr}."
                : $"{a.Firm} upgraded {ticker} to {a.ToGrade}{dateStr}.",
            "down" or "downgrade" or "downgraded" => hasGradeChange
                ? $"{a.Firm} downgraded {ticker} from {a.FromGrade} to {a.ToGrade}{dateStr}."
                : $"{a.Firm} downgraded {ticker} to {a.ToGrade}{dateStr}.",
            "init" or "initiated" or "initiation" => a.ToGrade is { } initGrade
                ? $"{a.Firm} initiated coverage on {ticker} at {initGrade}{dateStr}."
                : $"{a.Firm} initiated coverage on {ticker}{dateStr}.",
            "main" or "maintain" or "maintained" => a.ToGrade is { } mainGrade
                ? $"{a.Firm} maintained their {mainGrade} rating on {ticker}{dateStr}."
                : $"{a.Firm} maintained their rating on {ticker}{dateStr}.",
            "reit" or "reiterated" or "reiterate" => a.ToGrade is { } reitGrade
                ? $"{a.Firm} reiterated their {reitGrade} rating on {ticker}{dateStr}."
                : $"{a.Firm} reiterated their rating on {ticker}{dateStr}.",
            _ => a.ToGrade is { } grade
                ? $"{a.Firm}: {a.Action} - {ticker} rated {grade}{dateStr}."
                : $"{a.Firm}: {a.Action} on {ticker}{dateStr}."
        };
    }

    private void BuildTrendChart(IReadOnlyList<RecommendationTrendPoint>? trend)
    {
        TrendSeries.Clear();
        TrendXAxes.Clear();
        TrendYAxes.Clear();

        if (trend is null || trend.Count == 0) return;

        // Yahoo returns most-recent-first ("0m", "-1m", "-2m", "-3m") - reverse so the chart reads
        // oldest-to-newest left-to-right, like a normal time series.
        var ordered = trend.Reverse().ToList();
        var labels = ordered.Select(t => t.Period).ToArray();

        TrendSeries.Add(StackedColumn(ordered.Select(t => (double)(t.StrongSell ?? 0)).ToArray(), "Strong Sell", ChartPalette.Destructive));
        TrendSeries.Add(StackedColumn(ordered.Select(t => (double)(t.Sell ?? 0)).ToArray(), "Sell", ChartPalette.Downgrade));
        TrendSeries.Add(StackedColumn(ordered.Select(t => (double)(t.Hold ?? 0)).ToArray(), "Hold", ChartPalette.Warning));
        TrendSeries.Add(StackedColumn(ordered.Select(t => (double)(t.Buy ?? 0)).ToArray(), "Buy", ChartPalette.Upgrade));
        TrendSeries.Add(StackedColumn(ordered.Select(t => (double)(t.StrongBuy ?? 0)).ToArray(), "Strong Buy", ChartPalette.Positive));

        TrendXAxes.Add(new Axis
        {
            Labels = labels,
            LabelsPaint = new SolidColorPaint(ChartPalette.AxisText),
            SeparatorsPaint = new SolidColorPaint(ChartPalette.AxisLine) { StrokeThickness = 1 },
            TextSize = 11
        });
        TrendYAxes.Add(new Axis
        {
            LabelsPaint = new SolidColorPaint(ChartPalette.AxisText),
            SeparatorsPaint = new SolidColorPaint(ChartPalette.AxisLine) { StrokeThickness = 1 },
            TextSize = 11,
            MinLimit = 0
        });
    }

    private static StackedColumnSeries<double> StackedColumn(double[] values, string name, SKColor color) => new()
    {
        Values = values,
        Name = name,
        Fill = new SolidColorPaint(color),
        Stroke = null
    };
}
