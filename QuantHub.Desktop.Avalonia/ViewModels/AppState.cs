using CommunityToolkit.Mvvm.ComponentModel;

namespace QuantHub.Desktop.ViewModels;

/// <summary>Native replacement for the web app's TickerContext - the single active ticker, shared
/// across every page. Page ViewModels subscribe to PropertyChanged and re-fetch when it changes.</summary>
public sealed partial class AppState : ObservableObject
{
    [ObservableProperty]
    private string _activeTicker = "AAPL";
}
