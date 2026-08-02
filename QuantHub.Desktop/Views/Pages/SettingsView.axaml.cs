using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using QuantHub.Desktop.ViewModels.Pages;

namespace QuantHub.Desktop.Views.Pages;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;
        DataContextChanged -= OnDataContextChanged;

        this.FindControl<Button>("ExportWatchlistButton")!.Click += async (_, _) => await ExportAsync(vm);
        this.FindControl<Button>("ImportWatchlistButton")!.Click += async (_, _) => await ImportAsync(vm);
    }

    private async Task ExportAsync(SettingsViewModel vm)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null) return;

        try
        {
            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Watchlist",
                SuggestedFileName = "quant-terminal-watchlist.json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
            if (file is null) return;

            await using var stream = await file.OpenWriteAsync();
            var bytes = Encoding.UTF8.GetBytes(vm.ExportWatchlistJson());
            await stream.WriteAsync(bytes);
            ShowStatus("Watchlist exported.");
        }
        catch
        {
            ShowStatus("Export failed - the file may be in use or the location isn't writable.");
        }
    }

    private async Task ImportAsync(SettingsViewModel vm)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null) return;

        try
        {
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Watchlist",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
            if (files.Count == 0) return;

            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var json = await reader.ReadToEndAsync();

            var addedCount = vm.ImportWatchlistJson(json);
            ShowStatus(addedCount > 0
                ? $"Imported {addedCount} new ticker{(addedCount == 1 ? "" : "s")}."
                : "Nothing new to import - every ticker in that file is already on your watchlist.");
        }
        catch
        {
            ShowStatus("Import failed - the file may not be a valid watchlist export.");
        }
    }

    private void ShowStatus(string message)
    {
        var text = this.FindControl<TextBlock>("BackupStatusText")!;
        text.Text = message;
        text.IsVisible = true;
    }
}
