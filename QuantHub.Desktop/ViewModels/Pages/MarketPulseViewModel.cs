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

public sealed record MarketDetailRow(string Symbol, string Label, double ChangePct, double Change1wPct, double Change1mPct);

public sealed record SectorHeatCell(double Value, string Display, IBrush Background);

public sealed record SectorHeatRow(string Sector, SectorHeatCell OneDay, SectorHeatCell OneWeek, SectorHeatCell OneMonth);

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

    [ObservableProperty]
    private IReadOnlyList<string> _sectorNarrative = [];

    public ObservableCollection<ISeries> SectorSeries { get; } = [];
    public ObservableCollection<Axis> SectorXAxes { get; } = [];
    public ObservableCollection<Axis> SectorYAxes { get; } = [];

    [ObservableProperty]
    private IReadOnlyList<SectorHeatRow> _sectorHeatRows = [];

    public bool IsPro => _settings.IsPro;
    public bool IsBeginner => _settings.ViewMode == ViewMode.Beginner;
    public bool IsIntermediatePlus => !IsBeginner;

    public IBrush MoodBrush => Data?.MarketMood switch
    {
        "Extreme Fear" or "Fear" => ThemeResources.GetBrush("DestructiveBrush"),
        "Greed" or "Extreme Greed" => ThemeResources.GetBrush("PositiveBrush"),
        _ => ThemeResources.GetBrush("WarningBrush")
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
            BuildSectorNarrative(result.Sectors);
            BuildSectorHeatmap(result.Sectors);
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
            .Select(i => new MarketDetailRow(i.Symbol, i.Label, i.ChangePct, i.Change1wPct, i.Change1mPct))
            .ToList();
    }

    /// <summary>Beginner-only plain-English stand-in for the sector bar chart - top 2 leaders and
    /// bottom 2 laggards today, read as sentences instead of a technical chart (Sectors is already
    /// sorted descending by 1D % from MarketPulseService). Sign-aware phrasing since "leading" can
    /// still mean "down the least" on a broad down day.</summary>
    private void BuildSectorNarrative(IReadOnlyList<MarketPulseItem> sectors)
    {
        if (sectors.Count == 0) { SectorNarrative = []; return; }

        string Move(MarketPulseItem s) => s.ChangePct >= 0 ? $"up {s.ChangePct:0.00}%" : $"down {Math.Abs(s.ChangePct):0.00}%";

        var list = new List<string>();
        list.AddRange(sectors.Take(2).Select(s => $"{s.Label} is leading today, {Move(s)}."));
        list.AddRange(sectors.Reverse().Take(2).Select(s => $"{s.Label} is lagging today, {Move(s)}."));
        SectorNarrative = list;
    }

    /// <summary>Signature visual for this tab: a 1D/1W/1M sector grid colored by move magnitude,
    /// distinct from Peers' correlation heatmap (which colors by co-movement, -1..1) - here each
    /// column is scaled independently (1D saturates at +-5%, 1W at +-10%, 1M at +-20%) since monthly
    /// moves are naturally larger than daily ones; using one shared scale would leave every 1M cell
    /// looking equally maxed-out and hide real variation within that column.</summary>
    private void BuildSectorHeatmap(IReadOnlyList<MarketPulseItem> sectors)
    {
        SectorHeatRows = sectors.Select(s => new SectorHeatRow(
            s.Label,
            HeatCell(s.ChangePct, 5.0),
            HeatCell(s.Change1wPct, 10.0),
            HeatCell(s.Change1mPct, 20.0))).ToList();
    }

    private static SectorHeatCell HeatCell(double pct, double scaleMax) =>
        new(pct, $"{pct:+0.0;-0.0}%", HeatColor(pct, scaleMax));

    private static IBrush HeatColor(double pct, double scaleMax)
    {
        var neutral = ThemeResources.GetColor("PanelBorderBrush");
        var t = Math.Clamp(Math.Abs(pct) / scaleMax, 0, 1);
        var target = ThemeResources.GetColor(pct >= 0 ? "PositiveBrush" : "DestructiveBrush");
        return new SolidColorBrush(Lerp(neutral, target, t));
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));

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
