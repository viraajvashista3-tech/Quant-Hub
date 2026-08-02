using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using QuantHub.Core.Models;
using QuantHub.Desktop.Services;
using QuantHub.Desktop.ViewModels.Pages;

namespace QuantHub.Desktop.Views.Pages;

public partial class TerminalView : UserControl
{
    public TerminalView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>AsyncPopulator/SelectionChanged wired here rather than via XAML binding - same
    /// delegate-binding caution as ShellWindow.axaml.cs. Fires once: the page's DataContext is a DI
    /// singleton set once by the shell's DataTemplate, so there's nothing to re-wire on later changes.</summary>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not TerminalViewModel vm) return;
        DataContextChanged -= OnDataContextChanged;

        var compareBox = this.FindControl<AutoCompleteBox>("CompareBox")!;
        compareBox.AsyncPopulator = AutoCompletePopulator.Debounced(vm.SearchTickersAsync);
        compareBox.SelectionChanged += (_, _) =>
        {
            if (compareBox.SelectedItem is TickerSearchResult result) vm.SetCompareTickerCommand.Execute(result.Symbol);
        };
    }
}
