using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuantHub.Core.Portfolio;
using QuantHub.Desktop.Services;

namespace QuantHub.Desktop.ViewModels.Pages;

/// <summary>Portfolio page - tracks the user's own real positions (ticker/shares/entry price/entry
/// date they actually paid), distinct from the Track Record page (which grades the Quant Score's
/// own calls, not the user's decisions). Same honest, benchmark-relative methodology (excess return
/// vs SPY) as everywhere else in the app - see PortfolioCalculator.</summary>
public sealed partial class PortfolioViewModel : ObservableObject, IRefreshablePage
{
    private readonly PortfolioService _portfolio;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<PositionPerformance> Positions { get; } = [];

    [ObservableProperty]
    private PortfolioSummary _summary = new(0, 0, 0, 0, 0);

    public bool HasPositions => Positions.Count > 0;

    // ---- Add-position form fields - plain strings for the numeric inputs (not double/decimal)
    // so an in-progress, not-yet-valid keystroke (e.g. a bare "-" or "") doesn't need its own
    // sentinel value; AddPositionAsync below is where these actually get validated/parsed. ----
    [ObservableProperty]
    private string _newTicker = "";

    [ObservableProperty]
    private string _newShares = "";

    [ObservableProperty]
    private string _newEntryPrice = "";

    [ObservableProperty]
    private DateTimeOffset? _newEntryDate = DateTimeOffset.Now;

    [ObservableProperty]
    private string? _addErrorMessage;

    public PortfolioViewModel(PortfolioService portfolio)
    {
        _portfolio = portfolio;
        _portfolio.Changed += (_, _) => _ = LoadAsync();
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var results = await _portfolio.EvaluateAllAsync();
            Positions.Clear();
            foreach (var p in results.OrderByDescending(p => p.MarketValue)) Positions.Add(p);
            Summary = PortfolioCalculator.Summarize(results);
            OnPropertyChanged(nameof(HasPositions));

            if (results.Count == 0 && _portfolio.Positions.Count > 0)
            {
                ErrorMessage = "Couldn't fetch current prices for your positions - check your connection and refresh.";
            }
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

    [RelayCommand]
    private async Task AddPositionAsync()
    {
        AddErrorMessage = null;
        if (string.IsNullOrWhiteSpace(NewTicker)) { AddErrorMessage = "Enter a ticker."; return; }
        if (!double.TryParse(NewShares, out var shares) || shares <= 0) { AddErrorMessage = "Enter a valid number of shares."; return; }
        if (!double.TryParse(NewEntryPrice, out var entryPrice) || entryPrice <= 0) { AddErrorMessage = "Enter a valid entry price."; return; }
        if (NewEntryDate is not { } date) { AddErrorMessage = "Pick an entry date."; return; }

        IsBusy = true;
        try
        {
            var added = await _portfolio.AddPositionAsync(NewTicker, shares, entryPrice, DateOnly.FromDateTime(date.Date));
            if (!added)
            {
                AddErrorMessage = $"Couldn't add {NewTicker.Trim().ToUpperInvariant()} - check the ticker and try again.";
                return;
            }
            NewTicker = "";
            NewShares = "";
            NewEntryPrice = "";
            NewEntryDate = DateTimeOffset.Now;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RemovePosition(PositionPerformance position) => _portfolio.RemovePosition(position.Ticker, position.EntryDate);
}
