using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuantHub.Desktop.ViewModels.Pages;

namespace QuantHub.Desktop.ViewModels;

public sealed record WorkspaceTab(string Key, string Icon, string Label, IRefreshablePage Page);

/// <summary>Groups the five pages that are all just different views of the one active ticker
/// (Terminal/Fundamentals/Analyst/Peers/Insider) behind a single "Terminal" nav entry with an
/// internal tab strip, instead of five separate top-level nav items. Market Pulse stays a separate
/// top-level page since it's ticker-independent (see MarketPulseViewModel), and AI Research stays
/// separate since it's a chat surface, not a data tab.</summary>
public sealed partial class StockWorkspaceViewModel : ObservableObject, IRefreshablePage
{
    public IReadOnlyList<WorkspaceTab> Tabs { get; }

    [ObservableProperty]
    private WorkspaceTab _selectedTab;

    public StockWorkspaceViewModel(
        TerminalViewModel terminal,
        FundamentalsViewModel fundamentals,
        AnalystViewModel analyst,
        PeersViewModel peers,
        InsiderViewModel insider)
    {
        Tabs =
        [
            new WorkspaceTab("Terminal", "📈", "Terminal", terminal),
            new WorkspaceTab("Fundamentals", "📖", "Fundamentals", fundamentals),
            new WorkspaceTab("Analyst", "👥", "Analyst", analyst),
            new WorkspaceTab("Peers", "📊", "Peers", peers),
            new WorkspaceTab("Insider", "👁", "Insider", insider)
        ];
        _selectedTab = Tabs[0];
    }

    [RelayCommand]
    private void SelectTab(WorkspaceTab tab) => SelectedTab = tab;

    /// <summary>Refreshes every tab, not just the visible one - simpler and safer than tracking
    /// which tabs have gone stale while hidden, and matches how Universe's two independent sections
    /// both reload on its own RefreshCommand.</summary>
    [RelayCommand]
    private async Task RefreshAsync() =>
        await Task.WhenAll(Tabs.Select(t => t.Page.RefreshCommand.ExecuteAsync(null)));
}
