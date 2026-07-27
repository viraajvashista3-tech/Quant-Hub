using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using QuantHub.Desktop.Messages;
using QuantHub.Desktop.Services;
using QuantHub.Desktop.ViewModels.Pages;

namespace QuantHub.Desktop.ViewModels;

public sealed record NavItem(string Tag, string Label, string Icon, bool IsBeta = false);

/// <summary>
/// Shell composition root: fixed sidebar (ticker input + 8-item nav) + swappable content area,
/// replacing Wouter routing with a nav-selection-driven DataTemplate swap of CurrentPage.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly AppState _appState;
    private readonly SettingsService _settings;
    private readonly TerminalViewModel _terminal;
    private readonly UniverseViewModel _universe;
    private readonly FundamentalsViewModel _fundamentals;
    private readonly AnalystViewModel _analyst;
    private readonly PeersViewModel _peers;
    private readonly InsiderViewModel _insider;
    private readonly MarketPulseViewModel _marketPulse;
    private readonly AiResearchViewModel _aiResearch;
    private readonly ComingSoonViewModel _comingSoon;
    private readonly SettingsViewModel _settingsPage;
    private readonly DispatcherTimer _tickerDebounce = new() { Interval = TimeSpan.FromMilliseconds(350) };

    public IReadOnlyList<NavItem> NavItems { get; } =
    [
        new("Terminal", "Terminal", "📈"),
        new("Universe", "Universe", "🧭"),
        new("Analyst", "Analyst", "👥"),
        new("Peers", "Peers", "📊"),
        new("Fundamentals", "Fundamentals", "📖"),
        new("Insider", "Insider", "👁"),
        new("MarketPulse", "Market Pulse", "🌐"),
        new("Ai", "AI Research", "🤖", IsBeta: true)
    ];

    [ObservableProperty]
    private NavItem? _selectedNav;

    [ObservableProperty]
    private string _tickerInput;

    [ObservableProperty]
    private string _headerText = "Terminal";

    [ObservableProperty]
    private object _currentPage;

    public ShellViewModel(
        AppState appState,
        SettingsService settings,
        TerminalViewModel terminal,
        UniverseViewModel universe,
        FundamentalsViewModel fundamentals,
        AnalystViewModel analyst,
        PeersViewModel peers,
        InsiderViewModel insider,
        MarketPulseViewModel marketPulse,
        AiResearchViewModel aiResearch,
        ComingSoonViewModel comingSoon,
        SettingsViewModel settingsPage)
    {
        _appState = appState;
        _settings = settings;
        _terminal = terminal;
        _universe = universe;
        _fundamentals = fundamentals;
        _analyst = analyst;
        _peers = peers;
        _insider = insider;
        _marketPulse = marketPulse;
        _aiResearch = aiResearch;
        _comingSoon = comingSoon;
        _settingsPage = settingsPage;

        _selectedNav = NavItems[0];
        _tickerInput = appState.ActiveTicker;
        _currentPage = _terminal;

        _appState.PropertyChanged += OnAppStateChanged;
        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsService.ViewMode)) OnPropertyChanged(nameof(ViewModeLabel));
        };

        // Commit to AppState.ActiveTicker only after the user pauses typing - without this, typing
        // "SHEL" fires five separate loads (S/SH/SHE/SHEL), and since they're unordered fire-and-forget
        // calls, a slower earlier one can complete after a faster later one and overwrite the UI with
        // stale (or "not found") results for a ticker the user already moved on from.
        _tickerDebounce.Tick += (_, _) =>
        {
            _tickerDebounce.Stop();
            var upper = TickerInput.Trim().ToUpperInvariant();
            if (_appState.ActiveTicker != upper) _appState.ActiveTicker = upper;
        };

        WeakReferenceMessenger.Default.Register<NavigateToTickerMessage>(this, (_, m) =>
        {
            _tickerDebounce.Stop();
            TickerInput = m.Ticker;
            _appState.ActiveTicker = m.Ticker;
            SelectedNav = NavItems[0];
        });
    }

    public string ViewModeLabel => _settings.ViewMode.ToString();

    public bool IsSettingsActive => CurrentPage == _settingsPage;

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppState.ActiveTicker)) return;
        if (TickerInput != _appState.ActiveTicker) TickerInput = _appState.ActiveTicker;
    }

    partial void OnTickerInputChanged(string value)
    {
        _tickerDebounce.Stop();
        _tickerDebounce.Start();
    }

    partial void OnSelectedNavChanged(NavItem? value)
    {
        if (value is null) return;
        HeaderText = value.Label;
        CurrentPage = value.Tag switch
        {
            "Terminal" => _terminal,
            "Universe" => _universe,
            "Fundamentals" => _fundamentals,
            "Analyst" => _analyst,
            "Peers" => _peers,
            "Insider" => _insider,
            "MarketPulse" => _marketPulse,
            "Ai" => _aiResearch,
            _ => _comingSoon
        };
    }

    partial void OnCurrentPageChanged(object value) => OnPropertyChanged(nameof(IsSettingsActive));

    private ComingSoonViewModel WithMessage(string message)
    {
        _comingSoon.Message = message;
        return _comingSoon;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        SelectedNav = null;
        HeaderText = "Settings";
        CurrentPage = _settingsPage;
    }

    [RelayCommand]
    private void Refresh()
    {
        if (CurrentPage is IRefreshablePage page && page.RefreshCommand.CanExecute(null))
        {
            page.RefreshCommand.Execute(null);
        }
    }
}
