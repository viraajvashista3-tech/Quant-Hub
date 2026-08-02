using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using QuantHub.Core.Models;
using QuantHub.Core.Services;
using QuantHub.Desktop.Services;
using QuantHub.Desktop.Theming;

namespace QuantHub.Desktop.ViewModels.Pages;

public sealed partial class FundamentalsViewModel : ObservableObject, IRefreshablePage
{
    private readonly AppState _appState;
    private readonly StockAnalysisService _stockAnalysis;
    private readonly SettingsService _settings;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private FundamentalsData? _data;

    [ObservableProperty]
    private double? _currentPrice;

    [ObservableProperty]
    private EarningsData? _earnings;

    public ObservableCollection<ISeries> EarningsSeries { get; } = [];
    public ObservableCollection<Axis> EarningsXAxes { get; } = [];
    public ObservableCollection<Axis> EarningsYAxes { get; } = [];

    public bool HasEarningsHistory => Earnings?.History.Count > 0;

    public string? NextEarningsLabel => Earnings?.NextEarningsDate is { } d && DateTime.TryParse(d, out var parsed)
        ? parsed.ToString("MMM d, yyyy")
        : null;

    public bool IsPro => _settings.IsPro;
    public bool IsBeginner => _settings.ViewMode == ViewMode.Beginner;
    public bool IsIntermediatePlus => !IsBeginner;

    /// <summary>true = current price below the Graham Number (undervalued read); false = above (overvalued read); null when unavailable.</summary>
    public bool? IsBelowGraham => Data?.GrahamNumber is { } graham && CurrentPrice is { } price ? price < graham : null;

    public bool ShowBelowGraham => IsBelowGraham == true;
    public bool ShowAboveGraham => IsBelowGraham == false;

    public bool HasDividendData => Data?.DividendYield is not null || Data?.DividendRate is not null;

    /// <summary>Plain-English read on valuation/dividend/debt for Beginner mode. Thresholds are
    /// rough, commonly-cited rules of thumb (not sector-relative, since this page has no peer set to
    /// compare against) - explicitly framed as such rather than presented as precise judgments.</summary>
    public string? BeginnerSummary
    {
        get
        {
            if (Data is not { } d) return null;
            var sb = new StringBuilder();
            sb.Append(d.Name).Append(' ');

            if (d.Pe is { } pe and > 0)
            {
                var read = pe switch
                {
                    < 15 => "trades cheaply relative to its earnings",
                    <= 25 => "trades at a fairly typical valuation relative to its earnings",
                    _ => "trades at a rich valuation relative to its earnings"
                };
                sb.Append(read).Append($" (P/E of {pe:0.0}). ");
            }
            else
            {
                sb.Append("doesn't have a meaningful P/E ratio to judge valuation by. ");
            }

            sb.Append(d.DividendYield is { } dy and > 0
                ? $"It pays a dividend yielding about {dy * 100:0.0}% a year. "
                : "It does not currently pay a dividend. ");

            if (d.DebtToEquity is { } de)
            {
                var debtRead = de switch
                {
                    < 50 => "carries relatively low debt",
                    <= 150 => "carries a moderate amount of debt",
                    _ => "carries a high amount of debt"
                };
                sb.Append("The balance sheet ").Append(debtRead).Append($" (debt/equity of {de:0.0}).");
            }

            return sb.ToString().Trim();
        }
    }

    /// <summary>Beginner-only "quick facts" list - size, 52-week range, revenue trend, in plain
    /// language. Deliberately doesn't repeat dividend/valuation/debt, which BeginnerSummary already covers.</summary>
    public IReadOnlyList<string> BeginnerQuickFacts
    {
        get
        {
            if (Data is not { } d) return [];
            var list = new List<string>();
            if (d.MarketCap is { } mc) list.Add($"Market cap: {FormatLarge(mc)}.");
            if (d.FiftyTwoWeekHigh is { } hi && d.FiftyTwoWeekLow is { } lo)
                list.Add($"Over the past year it's traded between {lo:0.00} and {hi:0.00}.");
            if (d.RevenueGrowth is { } rg)
                list.Add(rg >= 0
                    ? $"Revenue is growing about {rg * 100:0.0}% a year."
                    : $"Revenue has been shrinking about {Math.Abs(rg) * 100:0.0}% a year.");
            return list;
        }
    }

    /// <summary>Actual vs. estimated EPS per quarter, oldest-to-newest left-to-right (History itself
    /// is stored newest-first, per EarningsAnalyzer) - a beat shows as the Actual bar taller than
    /// Estimate, a miss shows it shorter, without needing per-bar conditional coloring.</summary>
    private void BuildEarningsChart()
    {
        EarningsSeries.Clear();
        EarningsXAxes.Clear();
        EarningsYAxes.Clear();

        var quarters = Earnings?.History.Reverse().ToList();
        if (quarters is not { Count: > 0 }) return;

        var actual = quarters.Select(q => (double?)q.EpsActual).ToArray();
        var estimate = quarters.Select(q => (double?)q.EpsEstimate).ToArray();
        var labels = quarters.Select(q => DateTime.TryParse(q.Date, out var d) ? d.ToString("MMM yy") : q.Date).ToArray();

        EarningsSeries.Add(new ColumnSeries<double?>
        {
            Values = estimate, Name = "Estimate",
            Fill = new SolidColorPaint(ChartPalette.AxisLine)
        });
        EarningsSeries.Add(new ColumnSeries<double?>
        {
            Values = actual, Name = "Actual",
            Fill = new SolidColorPaint(ChartPalette.FundamentalsAccent)
        });

        EarningsXAxes.Add(new Axis
        {
            Labels = labels,
            LabelsPaint = new SolidColorPaint(ChartPalette.AxisText),
            SeparatorsPaint = new SolidColorPaint(ChartPalette.AxisLine) { StrokeThickness = 1 },
            TextSize = 11
        });
        EarningsYAxes.Add(new Axis
        {
            LabelsPaint = new SolidColorPaint(ChartPalette.AxisText),
            SeparatorsPaint = new SolidColorPaint(ChartPalette.AxisLine) { StrokeThickness = 1 },
            TextSize = 11
        });
    }

    private static string FormatLarge(double v) => v switch
    {
        >= 1e12 => $"${v / 1e12:0.00} trillion",
        >= 1e9 => $"${v / 1e9:0.0} billion",
        >= 1e6 => $"${v / 1e6:0.0} million",
        _ => $"${v:N0}"
    };

    public FundamentalsViewModel(AppState appState, StockAnalysisService stockAnalysis, SettingsService settings)
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
        };

        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var ticker = _appState.ActiveTicker;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var fundamentalsTask = _stockAnalysis.GetFundamentalsAsync(ticker);
            var overviewTask = _stockAnalysis.GetOverviewAsync(ticker);
            var earningsTask = _stockAnalysis.GetEarningsAsync(ticker);
            await Task.WhenAll(fundamentalsTask, overviewTask, earningsTask);

            Data = fundamentalsTask.Result;
            CurrentPrice = overviewTask.Result?.Price;
            Earnings = earningsTask.Result;
            OnPropertyChanged(nameof(IsBelowGraham));
            OnPropertyChanged(nameof(ShowBelowGraham));
            OnPropertyChanged(nameof(ShowAboveGraham));
            OnPropertyChanged(nameof(HasDividendData));
            OnPropertyChanged(nameof(BeginnerSummary));
            OnPropertyChanged(nameof(BeginnerQuickFacts));
            OnPropertyChanged(nameof(HasEarningsHistory));
            OnPropertyChanged(nameof(NextEarningsLabel));
            BuildEarningsChart();
            if (Data is null) ErrorMessage = $"No data found for {ticker}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
