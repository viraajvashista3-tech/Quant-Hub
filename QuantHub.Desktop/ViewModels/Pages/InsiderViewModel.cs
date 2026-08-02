using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using QuantHub.Core.Models;
using QuantHub.Core.Services;
using QuantHub.Desktop.Services;
using QuantHub.Desktop.Theming;

namespace QuantHub.Desktop.ViewModels.Pages;

/// <summary>Insider activity page - ownership split, net sentiment, 6-month purchase/sale summary,
/// and the classified transaction table. Beginner trades the raw table for plain sentences and hides
/// the ownership/6-month numeric cards; Pro sees more transaction rows than Intermediate.</summary>
public sealed partial class InsiderViewModel : ObservableObject, IRefreshablePage
{
    private readonly AppState _appState;
    private readonly StockAnalysisService _stockAnalysis;
    private readonly SettingsService _settings;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private InsiderData? _data;

    public ObservableCollection<ISeries> TimelineSeries { get; } = [];
    public ObservableCollection<Axis> TimelineXAxes { get; } = [];
    public ObservableCollection<Axis> TimelineYAxes { get; } = [];

    [ObservableProperty]
    private bool _hasTimeline;

    public bool IsPro => _settings.IsPro;
    public bool IsBeginner => _settings.ViewMode == ViewMode.Beginner;
    public bool IsIntermediatePlus => !IsBeginner;

    public IBrush SentimentBrush => Data?.NetSentiment switch
    {
        "Net Buyers" => ThemeResources.GetBrush("PositiveBrush"),
        "Net Sellers" => ThemeResources.GetBrush("DestructiveBrush"),
        _ => ThemeResources.GetBrush("MutedTextBrush")
    };

    public string? BeginnerSummary
    {
        get
        {
            if (Data is not { } d) return null;
            var sentiment = d.NetSentiment switch
            {
                "Net Buyers" => "have been buying more than selling",
                "Net Sellers" => "have been selling more than buying",
                _ => "have been roughly balanced between buying and selling"
            };
            var ownership = d.InsiderOwnership is { } io
                ? $" Company insiders own about {io * 100:0.0}% of the shares outstanding."
                : "";
            return $"Over the recorded transactions, company insiders {sentiment} ({d.BuyCount} buys vs {d.SellCount} sells).{ownership}";
        }
    }

    public IReadOnlyList<InsiderTransaction> VisibleTransactions =>
        Data?.Transactions.Take(IsPro ? 50 : 15).ToList() ?? [];

    public IReadOnlyList<string> NarrativeTransactions =>
        Data?.Transactions.Take(5).Select(BuildTransactionSentence).ToList() ?? [];

    private static string BuildTransactionSentence(InsiderTransaction t)
    {
        var dateStr = t.Date is { } d && DateTime.TryParse(d, out var parsed) ? $" on {parsed:MMM d, yyyy}" : "";
        var role = string.IsNullOrEmpty(t.Position) ? "" : $" ({t.Position})";
        var verb = t.TransactionType switch
        {
            "Purchase" => "bought",
            "Sale" => "sold",
            "Gift" => "gifted",
            "Option Exercise" => "exercised options for",
            "Award/Grant" => "was awarded",
            _ => "reported a transaction of"
        };
        var shares = t.Shares is { } sh ? $"{sh:N0} shares" : "shares";
        var value = t.Value is { } v and > 0 ? $" (worth about {v:C0})" : "";
        return $"{t.Insider}{role} {verb} {shares}{value}{dateStr}.";
    }

    public InsiderViewModel(AppState appState, StockAnalysisService stockAnalysis, SettingsService settings)
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
            OnPropertyChanged(nameof(VisibleTransactions));
            OnPropertyChanged(nameof(NarrativeTransactions));
        };

        _ = LoadAsync();
    }

    /// <summary>Signature visual for this tab: Purchase/Sale transactions plotted over time (X = days
    /// before today, Y = +1 Purchase / -1 Sale), distinct from the narrative sentences and raw table
    /// already on this page - reads at a glance whether recent activity clusters toward buying or
    /// selling, and whether it's recent or old.</summary>
    private void BuildTimeline()
    {
        TimelineSeries.Clear();
        TimelineXAxes.Clear();
        TimelineYAxes.Clear();

        var today = DateTime.UtcNow.Date;
        var dated = (Data?.Transactions ?? [])
            .Where(t => t.TransactionType is "Purchase" or "Sale" && t.Date is not null && DateTime.TryParse(t.Date, out _))
            .Select(t => (Transaction: t, DaysAgo: (today - DateTime.Parse(t.Date!)).TotalDays))
            .ToList();

        HasTimeline = dated.Count > 0;
        if (!HasTimeline) return;

        var purchases = dated.Where(d => d.Transaction.TransactionType == "Purchase")
            .Select(d => new ObservablePoint(-d.DaysAgo, 1)).ToList();
        var sales = dated.Where(d => d.Transaction.TransactionType == "Sale")
            .Select(d => new ObservablePoint(-d.DaysAgo, -1)).ToList();

        if (purchases.Count > 0)
        {
            TimelineSeries.Add(new ScatterSeries<ObservablePoint>
            {
                Values = purchases, Name = "Purchase",
                Fill = new SolidColorPaint(ChartPalette.Positive),
                Stroke = null,
                GeometrySize = 16
            });
        }
        if (sales.Count > 0)
        {
            TimelineSeries.Add(new ScatterSeries<ObservablePoint>
            {
                Values = sales, Name = "Sale",
                Fill = new SolidColorPaint(ChartPalette.Destructive),
                Stroke = null,
                GeometrySize = 16
            });
        }

        TimelineXAxes.Add(new Axis
        {
            Name = "Days ago",
            LabelsPaint = new SolidColorPaint(ChartPalette.AxisText),
            SeparatorsPaint = new SolidColorPaint(ChartPalette.AxisLine) { StrokeThickness = 1 },
            TextSize = 11,
            Labeler = v => $"{-v:0}"
        });
        TimelineYAxes.Add(new Axis
        {
            LabelsPaint = new SolidColorPaint(ChartPalette.AxisText),
            SeparatorsPaint = new SolidColorPaint(ChartPalette.AxisLine) { StrokeThickness = 1 },
            TextSize = 11,
            MinLimit = -1.5,
            MaxLimit = 1.5,
            Labeler = v => v switch { 1 => "Buy", -1 => "Sell", _ => "" }
        });
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
            var result = await _stockAnalysis.GetInsiderAsync(ticker, ct);
            if (ct.IsCancellationRequested) return;

            Data = result;
            OnPropertyChanged(nameof(SentimentBrush));
            OnPropertyChanged(nameof(BeginnerSummary));
            OnPropertyChanged(nameof(VisibleTransactions));
            OnPropertyChanged(nameof(NarrativeTransactions));
            BuildTimeline();

            if (Data is null) ErrorMessage = $"No insider data found for {ticker}.";
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
}
