using CommunityToolkit.Mvvm.ComponentModel;
using QuantHub.Desktop.Services;

namespace QuantHub.Desktop.ViewModels;

/// <summary>Native replacement for the web app's TickerContext - the single active ticker, shared
/// across every page. Page ViewModels subscribe to PropertyChanged and re-fetch when it changes.
/// Initializes from, and keeps in sync with, SettingsService.LastTicker so the app reopens on
/// whatever ticker was last active instead of always resetting to AAPL.</summary>
public sealed partial class AppState : ObservableObject
{
    private readonly SettingsService _settings;

    [ObservableProperty]
    private string _activeTicker;

    public AppState(SettingsService settings)
    {
        _settings = settings;
        _activeTicker = string.IsNullOrWhiteSpace(settings.LastTicker) ? "AAPL" : settings.LastTicker;
    }

    partial void OnActiveTickerChanged(string value)
    {
        _settings.LastTicker = value;
        _settings.Save();
    }
}
