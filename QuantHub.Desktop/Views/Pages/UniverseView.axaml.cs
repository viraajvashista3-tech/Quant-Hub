using System;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using QuantHub.Core.Models;
using QuantHub.Desktop.Services;
using QuantHub.Desktop.ViewModels.Pages;

namespace QuantHub.Desktop.Views.Pages;

public partial class UniverseView : UserControl
{
    public UniverseView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not UniverseViewModel vm) return;
        DataContextChanged -= OnDataContextChanged;

        var addBox = this.FindControl<AutoCompleteBox>("AddBox")!;
        addBox.AsyncPopulator = AutoCompletePopulator.Debounced(vm.SearchTickersAsync);
        addBox.SelectionChanged += (_, _) =>
        {
            if (addBox.SelectedItem is TickerSearchResult result) vm.AddTickerCommand.Execute(result.Symbol);
        };

        this.FindControl<Button>("ExportWatchlistButton")!.Click += async (_, _) =>
            await ExportCsvAsync("watchlist.csv", UniverseViewModel.BuildWatchlistCsv(vm.WatchlistRows));
        this.FindControl<Button>("ExportTop20Button")!.Click += async (_, _) =>
            await ExportCsvAsync("universe-top20.csv", UniverseViewModel.BuildTop20Csv(vm.Top20Rows));
    }

    /// <summary>Native save-file dialog via Avalonia's StorageProvider (not a fixed path) - lets the
    /// user pick where the export goes, same as any other desktop app's "Save As". Best-effort: a
    /// cancelled dialog or a write failure (e.g. no permission to the chosen folder) both just no-op
    /// rather than surface an error banner - exporting isn't part of the page's core data-loading
    /// path, so failures here don't need the same ErrorMessage treatment as a failed fetch.</summary>
    private async Task ExportCsvAsync(string suggestedFileName, string csvContent)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider) return;

        try
        {
            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export CSV",
                SuggestedFileName = suggestedFileName,
                FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }]
            });
            if (file is null) return;

            await using var stream = await file.OpenWriteAsync();
            var bytes = Encoding.UTF8.GetBytes(csvContent);
            await stream.WriteAsync(bytes);
        }
        catch
        {
            // best-effort - a cancelled dialog or a write failure just no-ops
        }
    }
}
