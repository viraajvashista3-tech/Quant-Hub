using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using QuantHub.Core.Models;
using QuantHub.Desktop.Services;
using QuantHub.Desktop.ViewModels;

namespace QuantHub.Desktop.Views;

public partial class ShellWindow : Window
{
    private readonly ScrollViewer _contentScrollViewer;

    public ShellWindow(ShellViewModel viewModel)
    {
        AvaloniaXamlLoader.Load(this);
        _contentScrollViewer = this.FindControl<ScrollViewer>("ContentScrollViewer")!;
        DataContext = viewModel;

        // Swapping ContentControl.Content leaves the ScrollViewer at its previous scroll offset,
        // so a page navigated to from partway down a taller page renders with its top clipped
        // behind the fixed header bar until the user manually scrolls up.
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // AsyncPopulator/SelectionChanged are wired here rather than via XAML binding - delegate-typed
        // property binding is a known soft spot in this codebase's Avalonia version (see
        // avalonia_migration_gotchas memory), so this sidesteps it entirely.
        var tickerBox = this.FindControl<AutoCompleteBox>("TickerBox")!;
        tickerBox.AsyncPopulator = AutoCompletePopulator.Debounced(viewModel.SearchTickersAsync);
        tickerBox.SelectionChanged += (_, _) =>
        {
            if (tickerBox.SelectedItem is TickerSearchResult result) viewModel.CommitTicker(result.Symbol);
        };

        // "/" jumps to the ticker search from anywhere in the app (same convention as GitHub/Slack/
        // most modern web apps) - skipped whenever the key originated inside a text input, so typing
        // a literal "/" into the Watchlist add-ticker box, Terminal's compare-to box, etc. isn't hijacked.
        // Tunnel routing (not the simpler bubbling KeyDown +=) is required here: an AutoCompleteBox
        // with focus swallows the Window's bubble-phase KeyDown entirely (see
        // avalonia_migration_gotchas gotcha #7 - the same reason the F12 debug-screenshot hook can
        // silently fail to fire) - since TickerBox is itself an AutoCompleteBox, a bubbling handler
        // would make this shortcut unreliable from the moment the app opens. Tunnel fires top-down
        // before that swallowing happens.
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key != Key.OemQuestion || e.KeyModifiers != KeyModifiers.None) return;
            if (e.Source is TextBox or AutoCompleteBox) return;
            tickerBox.Focus();
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.CurrentPage))
        {
            _contentScrollViewer.Offset = new Vector(0, 0);
        }
    }
}
