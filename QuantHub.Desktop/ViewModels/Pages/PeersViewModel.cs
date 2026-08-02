using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using QuantHub.Core.Models;
using QuantHub.Core.Services;
using QuantHub.Desktop.Messages;
using QuantHub.Desktop.Services;
using QuantHub.Desktop.Theming;
using SkiaSharp;

namespace QuantHub.Desktop.ViewModels.Pages;

public sealed record PeerRow(PeerStock Stock, bool IsSubject);

public sealed record CorrelationCell(string Ticker, double? Value, string Display, IBrush Background);

public sealed record CorrelationRow(string Ticker, IReadOnlyList<CorrelationCell> Cells);

/// <summary>Peer comparison page - sector fundamentals table, a P/E comparison chart, a concluding
/// summary, and a correlation heatmap (Pro-only). Beginner keeps the chart and conclusion but drops
/// the dense table down to three columns; Pro adds the correlation matrix on top of Intermediate.</summary>
public sealed partial class PeersViewModel : ObservableObject, IRefreshablePage
{
    private readonly AppState _appState;
    private readonly StockAnalysisService _stockAnalysis;
    private readonly SettingsService _settings;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private PeersData? _data;

    [ObservableProperty]
    private IReadOnlyList<PeerRow> _rows = [];

    [ObservableProperty]
    private IReadOnlyList<CorrelationRow> _correlationRows = [];

    [ObservableProperty]
    private IReadOnlyList<string> _correlationHeaderTickers = [];

    public ObservableCollection<ISeries> ComparisonSeries { get; } = [];
    public ObservableCollection<Axis> ComparisonXAxes { get; } = [];
    public ObservableCollection<Axis> ComparisonYAxes { get; } = [];

    public ObservableCollection<ISeries> ValuationScatterSeries { get; } = [];
    public ObservableCollection<Axis> ValuationScatterXAxes { get; } = [];
    public ObservableCollection<Axis> ValuationScatterYAxes { get; } = [];

    [ObservableProperty]
    private bool _hasValuationScatter;

    public bool IsPro => _settings.IsPro;
    public bool IsBeginner => _settings.ViewMode == ViewMode.Beginner;
    public bool IsIntermediatePlus => !IsBeginner;

    public PeersViewModel(AppState appState, StockAnalysisService stockAnalysis, SettingsService settings)
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

    [RelayCommand]
    private void SelectTicker(string ticker) =>
        WeakReferenceMessenger.Default.Send(new NavigateToTickerMessage(ticker));

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
            var result = await _stockAnalysis.GetPeersAsync(ticker, "1y", ct);
            if (ct.IsCancellationRequested) return;

            Data = result;
            Rows = result.Peers.Select(p => new PeerRow(p, p.Ticker == ticker.ToUpperInvariant())).ToList();
            BuildCorrelation(result.CorrelationMatrix);
            BuildComparisonChart(Rows);
            BuildValuationScatter(Rows);

            if (result.Peers.Count == 0) ErrorMessage = $"No peer data found for {ticker}.";
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

    /// <summary>P/E is the one valuation metric almost every peer reports, so it doubles as the
    /// default "how does this stock compare" visual - one bar per company, subject picked out in
    /// the accent color so it reads at a glance against the muted peer bars.</summary>
    private void BuildComparisonChart(IReadOnlyList<PeerRow> rows)
    {
        ComparisonSeries.Clear();
        ComparisonXAxes.Clear();
        ComparisonYAxes.Clear();
        if (rows.Count == 0) return;

        var labels = rows.Select(r => r.Stock.Ticker).ToArray();

        for (var i = 0; i < rows.Count; i++)
        {
            var values = new double?[rows.Count];
            values[i] = rows[i].Stock.Pe;
            var color = rows[i].IsSubject ? ChartPalette.Primary : ChartPalette.AxisText;
            ComparisonSeries.Add(new ColumnSeries<double?>
            {
                Values = values,
                Name = rows[i].Stock.Ticker,
                Fill = new SolidColorPaint(color),
                Stroke = null,
                MaxBarWidth = 46
            });
        }

        ComparisonXAxes.Add(new Axis
        {
            Labels = labels,
            LabelsPaint = new SolidColorPaint(ChartPalette.AxisText),
            SeparatorsPaint = new SolidColorPaint(ChartPalette.AxisLine) { StrokeThickness = 1 },
            TextSize = 11
        });
        ComparisonYAxes.Add(new Axis
        {
            LabelsPaint = new SolidColorPaint(ChartPalette.AxisText),
            SeparatorsPaint = new SolidColorPaint(ChartPalette.AxisLine) { StrokeThickness = 1 },
            TextSize = 11,
            Labeler = v => $"{v:0.0}x",
            MinLimit = 0
        });
    }

    /// <summary>Signature visual for this tab: P/E (valuation) vs. ROE (quality) - a scatter, distinct
    /// from the bar chart and correlation heatmap already on this page. Two series (muted peers, accent
    /// subject) rather than one uniformly-colored series, so the subject stock still reads at a glance
    /// the way it does elsewhere on this page (highlighted table row, accent-colored P/E bar).</summary>
    private void BuildValuationScatter(IReadOnlyList<PeerRow> rows)
    {
        ValuationScatterSeries.Clear();
        ValuationScatterXAxes.Clear();
        ValuationScatterYAxes.Clear();

        bool Valid(PeerRow r) => r.Stock.Pe is > 0 && r.Stock.ReturnOnEquity is not null;
        var peerPoints = rows.Where(r => !r.IsSubject && Valid(r))
            .Select(r => new ObservablePoint(r.Stock.Pe, r.Stock.ReturnOnEquity!.Value * 100)).ToList();
        var subjectPoints = rows.Where(r => r.IsSubject && Valid(r))
            .Select(r => new ObservablePoint(r.Stock.Pe, r.Stock.ReturnOnEquity!.Value * 100)).ToList();

        HasValuationScatter = peerPoints.Count + subjectPoints.Count > 0;
        if (!HasValuationScatter) return;

        ValuationScatterSeries.Add(new ScatterSeries<ObservablePoint>
        {
            Values = peerPoints, Name = "Peers",
            Fill = new SolidColorPaint(ChartPalette.AxisText),
            Stroke = null,
            GeometrySize = 12
        });
        ValuationScatterSeries.Add(new ScatterSeries<ObservablePoint>
        {
            Values = subjectPoints, Name = "This stock",
            Fill = new SolidColorPaint(ChartPalette.ChartAccent3),
            Stroke = null,
            GeometrySize = 20
        });

        ValuationScatterXAxes.Add(new Axis
        {
            Name = "P/E",
            LabelsPaint = new SolidColorPaint(ChartPalette.AxisText),
            SeparatorsPaint = new SolidColorPaint(ChartPalette.AxisLine) { StrokeThickness = 1 },
            TextSize = 11,
            Labeler = v => $"{v:0.0}x"
        });
        ValuationScatterYAxes.Add(new Axis
        {
            Name = "ROE",
            LabelsPaint = new SolidColorPaint(ChartPalette.AxisText),
            SeparatorsPaint = new SolidColorPaint(ChartPalette.AxisLine) { StrokeThickness = 1 },
            TextSize = 11,
            Labeler = v => $"{v:0}%"
        });
    }

    private void BuildCorrelation(IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>? matrix)
    {
        if (matrix is null || matrix.Count == 0) { CorrelationRows = []; CorrelationHeaderTickers = []; return; }

        var tickers = matrix.Keys.ToList();
        CorrelationHeaderTickers = tickers;
        var rows = new List<CorrelationRow>();
        foreach (var rowTicker in tickers)
        {
            var cells = new List<CorrelationCell>();
            foreach (var colTicker in tickers)
            {
                var value = matrix[rowTicker].TryGetValue(colTicker, out var v) ? v : (double?)null;
                cells.Add(new CorrelationCell(colTicker, value, value is { } dv ? dv.ToString("0.00") : "-", CorrelationColor(value)));
            }
            rows.Add(new CorrelationRow(rowTicker, cells));
        }
        CorrelationRows = rows;
    }

    /// <summary>Resolves the live value of an Avalonia brush resource each call (rather than caching
    /// once), so the heatmap's interpolated colors stay correct if the theme changes.</summary>
    private static Color ResolveColor(string key) => ThemeResources.GetColor(key);

    private static IBrush CorrelationColor(double? value)
    {
        var neutral = ResolveColor("PanelBorderBrush");
        if (value is not { } v) return new SolidColorBrush(neutral);
        var t = Math.Clamp(Math.Abs(v), 0, 1);
        var target = v >= 0 ? ResolveColor("PositiveBrush") : ResolveColor("DestructiveBrush");
        return new SolidColorBrush(Lerp(neutral, target, t));
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));
}
