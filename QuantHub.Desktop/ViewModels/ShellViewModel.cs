using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using QuantHub.Core.Backtesting;
using QuantHub.Core.Models;
using QuantHub.Core.Services;
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
    private readonly StockAnalysisService _stockAnalysis;
    private readonly TerminalViewModel _terminal;
    private readonly UniverseViewModel _universe;
    private readonly FundamentalsViewModel _fundamentals;
    private readonly AnalystViewModel _analyst;
    private readonly PeersViewModel _peers;
    private readonly InsiderViewModel _insider;
    private readonly MarketPulseViewModel _marketPulse;
    private readonly SettingsViewModel _settingsPage;
    private readonly TrackRecordViewModel _trackRecord;
    private readonly PredictionLogService _predictionLog;
    private readonly DispatcherTimer _tickerDebounce = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly DispatcherTimer _autoRefreshTimer = new();

    public IReadOnlyList<NavItem> NavItems { get; } =
    [
        new("Terminal", "Terminal", "📈"),
        new("Universe", "Universe", "🧭"),
        new("Analyst", "Analyst", "👥"),
        new("Peers", "Peers", "📊"),
        new("Fundamentals", "Fundamentals", "📖"),
        new("Insider", "Insider", "👁"),
        new("MarketPulse", "Market Pulse", "🌐"),
        new("TrackRecord", "Track Record", "🔍")
    ];

    public IReadOnlyList<ViewModeOption> ViewModeOptions => SettingsService.ViewModeOptions;

    public ViewMode ViewMode => _settings.ViewMode;

    /// <summary>A small, honest trust signal built from PredictionLogService's live forward-tested
    /// Buy calls (see PredictionLog.ComputeStats) - deliberately just one number with a tooltip for
    /// context, not a resurrection of the "Backtest" page (removed from nav by explicit prior
    /// request - see backtest_feature memory update #10 - to keep recalibration mechanics fully
    /// automatic and hidden). Null (badge hidden) until at least one logged prediction has matured
    /// and been evaluated, rather than showing a misleading 0%-of-0 stat on a fresh install.</summary>
    public string? TrackRecordText
    {
        get
        {
            var buy = PredictionLog.ComputeStats(_predictionLog.Entries).FirstOrDefault(s => s.Signal == Signal.Buy);
            return buy is { Count: > 0, HitRatePct: { } hitRate }
                ? $"📊 Buy calls beat the S&P 500 {hitRate:0}% of the time ({buy.Count} evaluated)"
                : null;
        }
    }

    /// <summary>"What changed since your last visit" - watchlist Signal changes detected by
    /// SessionBriefingService, sent up from UniverseViewModel via WatchlistBriefingMessage so it can
    /// show at the shell level (visible regardless of which page happens to be open) rather than only
    /// on Universe. Dismissible; not re-shown for the same change once dismissed since
    /// SessionBriefingService already committed the new baseline the moment it computed this diff.</summary>
    [ObservableProperty]
    private IReadOnlyList<string> _briefingMessages = [];

    public bool HasBriefing => BriefingMessages.Count > 0;

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
        StockAnalysisService stockAnalysis,
        TerminalViewModel terminal,
        UniverseViewModel universe,
        FundamentalsViewModel fundamentals,
        AnalystViewModel analyst,
        PeersViewModel peers,
        InsiderViewModel insider,
        MarketPulseViewModel marketPulse,
        SettingsViewModel settingsPage,
        TrackRecordViewModel trackRecord,
        PredictionLogService predictionLog)
    {
        _appState = appState;
        _settings = settings;
        _stockAnalysis = stockAnalysis;
        _terminal = terminal;
        _universe = universe;
        _fundamentals = fundamentals;
        _analyst = analyst;
        _peers = peers;
        _insider = insider;
        _marketPulse = marketPulse;
        _settingsPage = settingsPage;
        _trackRecord = trackRecord;
        _predictionLog = predictionLog;
        _predictionLog.Updated += (_, _) => OnPropertyChanged(nameof(TrackRecordText));

        _tickerInput = appState.ActiveTicker;
        _currentPage = _terminal; // safe default; overwritten below via the SelectedNav setter if a different startup page was chosen

        _appState.PropertyChanged += OnAppStateChanged;
        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsService.ViewMode)) OnPropertyChanged(nameof(ViewMode));
            if (e.PropertyName == nameof(SettingsService.AutoRefreshInterval)) ApplyAutoRefreshInterval(_settings.AutoRefreshInterval);
        };

        // Silently re-runs whatever page is currently open's own RefreshCommand on the configured
        // interval - for anyone who leaves the app on a second monitor. Off by default; reconfigured
        // above whenever the Settings page changes AutoRefreshInterval, not just once at startup.
        _autoRefreshTimer.Tick += (_, _) => Refresh();
        ApplyAutoRefreshInterval(_settings.AutoRefreshInterval);

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

        WeakReferenceMessenger.Default.Register<WatchlistBriefingMessage>(this, (_, m) => BriefingMessages = m.Changes);

        // Resolved last (property setter, not the backing field) so OnSelectedNavChanged's existing
        // tag->page switch below is the one and only place that maps a tag to its page - avoids a
        // second, easy-to-forget-to-update copy of that mapping just for the startup case.
        var startupTag = ResolveStartupNavTag(_settings.StartupPage, _settings.LastViewedNavTag);
        SelectedNav = NavItems.FirstOrDefault(n => n.Tag == startupTag) ?? NavItems[0];
    }

    /// <summary>Pure mapping from the Settings page's "Start on" choice to a nav tag - pulled out of
    /// the constructor so it's directly unit-testable without constructing a full ShellViewModel
    /// (which needs every page ViewModel as a dependency).</summary>
    public static string ResolveStartupNavTag(StartupPage startupPage, string lastViewedNavTag) => startupPage switch
    {
        StartupPage.Terminal => "Terminal",
        StartupPage.Universe => "Universe",
        StartupPage.TrackRecord => "TrackRecord",
        _ => lastViewedNavTag
    };

    /// <summary>Backs the sidebar ticker AutoCompleteBox's AsyncPopulator (wired in
    /// ShellWindow.axaml.cs code-behind, not a XAML delegate binding).</summary>
    public Task<IReadOnlyList<TickerSearchResult>> SearchTickersAsync(string query, CancellationToken ct) =>
        _stockAnalysis.SearchTickersAsync(query, ct);

    /// <summary>Called from code-behind when the user picks a suggestion from the dropdown - commits
    /// immediately instead of waiting out the debounce, since an explicit pick is already a completed
    /// decision (unlike free-typed text, which might still be mid-word).</summary>
    public void CommitTicker(string symbol)
    {
        _tickerDebounce.Stop();
        var upper = symbol.Trim().ToUpperInvariant();
        TickerInput = upper;
        if (_appState.ActiveTicker != upper) _appState.ActiveTicker = upper;
    }

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
            "TrackRecord" => _trackRecord,
            _ => throw new InvalidOperationException($"No page registered for nav tag \"{value.Tag}\".")
        };

        _settings.LastViewedNavTag = value.Tag;
        _settings.Save();
    }

    partial void OnCurrentPageChanged(object value) => OnPropertyChanged(nameof(IsSettingsActive));

    partial void OnBriefingMessagesChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(HasBriefing));

    [RelayCommand]
    private void DismissBriefing() => BriefingMessages = [];

    [RelayCommand]
    private void SelectViewMode(ViewModeOption option) => _settings.ViewMode = option.Mode;

    [RelayCommand]
    private void OpenSettings()
    {
        SelectedNav = null;
        HeaderText = "Settings";
        CurrentPage = _settingsPage;
    }

    /// <summary>Backs the sidebar's track-record badge - clicking the one-line summary jumps straight
    /// to the full honesty page instead of the badge being a dead-end text label.</summary>
    [RelayCommand]
    private void OpenTrackRecord() => SelectedNav = NavItems.First(n => n.Tag == "TrackRecord");

    [RelayCommand]
    private void Refresh()
    {
        if (CurrentPage is IRefreshablePage page && page.RefreshCommand.CanExecute(null))
        {
            page.RefreshCommand.Execute(null);
        }
    }

    private void ApplyAutoRefreshInterval(AutoRefreshInterval interval)
    {
        _autoRefreshTimer.Stop();
        if (interval == AutoRefreshInterval.Off) return;
        _autoRefreshTimer.Interval = SettingsService.ToTimeSpan(interval);
        _autoRefreshTimer.Start();
    }
}
