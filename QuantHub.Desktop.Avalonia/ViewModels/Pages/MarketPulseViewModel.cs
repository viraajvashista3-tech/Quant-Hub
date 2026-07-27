using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using QuantHub.Core.MarketPulse;
using QuantHub.Core.Models;
using QuantHub.Desktop.Services;
using QuantHub.Desktop.Theming;
using SkiaSharp;

namespace QuantHub.Desktop.ViewModels.Pages;

public sealed record MarketDetailRow(string Label, double ChangePct, double Change1wPct, double Change1mPct);

/// <summary>Market-wide snapshot page - VIX-based mood, major indices, a sector-performance bar
/// chart, and macro instruments. Unlike every other page this one is ticker-independent, so it only
/// reloads on refresh, not on AppState.ActiveTicker changes. Beginner keeps just the mood/rotation
/// read in plain language; Pro adds a combined 1D/1W/1M detail table on top of the Intermediate view.</summary>
public sealed partial class MarketPulseViewModel : ObservableObject, IRefreshablePage
{
    private readonly MarketPulseService _marketPulse;
    private readonly SettingsService _settings;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private MarketPulseData? _data;

    [ObservableProperty]
    private IReadOnlyList<MarketDetailRow> _detailRows = [];

    public ObservableCollection<ISeries> SectorSeries { get; } = [];
    public ObservableCollection<Axis> SectorXAxes { get; } = [];
    public ObservableCollection<Axis> SectorYAxes { get; } = [];

    public bool IsPro => _settings.IsPro;
    public bool IsBeginner => _settings.ViewMode == ViewMode.Beginner;
    public bool IsIntermediatePlus => !IsBeginner;

    public IBrush MoodBrush => Data?.MarketMood switch
    {
        "Extreme Fear" or "Fear" => (IBrush)Avalonia.Application.Current!.Resources["DestructiveBrush"]!,
        "Greed" or "Extreme Greed" => (IBrush)Avalonia.Application.Current!.Resources["PositiveBrush"]!,
        _ => (IBrush)Avalonia.Application.Current!.Resources["WarningBrush"]!
    };

    public string? BeginnerSummary
    {
        get
        {
            if (Data is not { } d) return null;
            var moodRead = d.MarketMood switch
            {
                "Extreme Fear" => "Investors are extremely fearful right now",
                "Fear" => "Investors are fairly cautious right now",
                "Greed" => "Investors are fairly optimistic right now",
                "Extreme Greed" => "Investors are extremely optimistic right now",
                _ => "The overall mood is fairly neutral right now"
            };
            return $"{moodRead}, based on how much the market is swinging day to day (VIX {d.Vix:0.0}). {d.RotationNote}";
        }
    }

    public MarketPulseViewModel(MarketPulseService marketPulse, SettingsService settings)
    {
        _marketPulse = marketPulse;
        _settings = settings;

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

    private CancellationTokenSource? _loadCts;

    private async Task LoadAsync()
    {
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var ct = cts.Token;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _marketPulse.GetMarketPulseAsync(ct);
            if (ct.IsCancellationRequested) return;

            Data = result;
            OnPropertyChanged(nameof(MoodBrush));
            OnPropertyChanged(nameof(BeginnerSummary));
            BuildSectorChart(result.Sectors);
            BuildDetailRows(result);
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

    private void BuildDetailRows(MarketPulseData data)
    {
        DetailRows = data.Indices.Concat(data.Sectors).Concat(data.Macro)
            .Select(i => new MarketDetailRow(i.Label, i.ChangePct, i.Change1wPct, i.Change1mPct))
            .ToList();
    }

    private void BuildSectorChart(IReadOnlyList<MarketPulseItem> sectors)
    {
        SectorSeries.Clear();
        SectorXAxes.Clear();
        SectorYAxes.Clear();
        if (sectors.Count == 0) return;

        // Reverse so the strongest sector (already sorted descending by MarketPulseService) plots
        // at the top of the horizontal bar chart, matching how a leaderboard reads top-to-bottom.
        var ordered = sectors.Reverse().ToList();
        var values = ordered.Select(s => s.ChangePct).ToArray();
        var labels = ordered.Select(s => s.Label).ToArray();

        SectorXAxes.Add(new Axis
        {
            LabelsPaint = new SolidColorPaint(ChartPalette.AxisText),
            SeparatorsPaint = new SolidColorPaint(ChartPalette.AxisLine) { StrokeThickness = 1 },
            TextSize = 11,
            Labeler = v => $"{v:0.0}%"
        });
        SectorYAxes.Add(new Axis
        {
            Labels = labels,
            LabelsPaint = new SolidColorPaint(ChartPalette.AxisText),
            SeparatorsPaint = new SolidColorPaint(ChartPalette.AxisLine) { StrokeThickness = 1 },
            TextSize = 11
        });

        // Per-bar coloring (green gaining, red losing) needs each bar as its own single-value
        // series - LiveCharts2's row/column series only takes one Fill for the whole series.
        for (var i = 0; i < ordered.Count; i++)
        {
            var row = new double?[ordered.Count];
            row[i] = values[i];
            var color = values[i] >= 0 ? ChartPalette.Positive : ChartPalette.Destructive;
            SectorSeries.Add(new RowSeries<double?>
            {
                Values = row,
                Fill = new SolidColorPaint(color),
                Stroke = null,
                MaxBarWidth = 18
            });
        }
    }
}
