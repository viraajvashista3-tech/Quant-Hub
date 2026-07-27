using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuantHub.Core.Models;
using QuantHub.Core.Services;
using QuantHub.Desktop.Services;

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

    public bool IsPro => _settings.IsPro;
    public bool IsBeginner => _settings.ViewMode == ViewMode.Beginner;
    public bool IsIntermediatePlus => !IsBeginner;

    /// <summary>true = current price below the Graham Number (undervalued read); false = above (overvalued read); null when unavailable.</summary>
    public bool? IsBelowGraham => Data?.GrahamNumber is { } graham && CurrentPrice is { } price ? price < graham : null;

    public bool ShowBelowGraham => IsBelowGraham == true;
    public bool ShowAboveGraham => IsBelowGraham == false;

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
            await Task.WhenAll(fundamentalsTask, overviewTask);

            Data = fundamentalsTask.Result;
            CurrentPrice = overviewTask.Result?.Price;
            OnPropertyChanged(nameof(IsBelowGraham));
            OnPropertyChanged(nameof(ShowBelowGraham));
            OnPropertyChanged(nameof(ShowAboveGraham));
            OnPropertyChanged(nameof(BeginnerSummary));
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
