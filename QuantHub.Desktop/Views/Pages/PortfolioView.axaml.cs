using System;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using QuantHub.Desktop.ViewModels.Pages;

namespace QuantHub.Desktop.Views.Pages;

public partial class PortfolioView : UserControl
{
    public PortfolioView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not PortfolioViewModel vm) return;
        DataContextChanged -= OnDataContextChanged;

        this.FindControl<Button>("ExportCsvButton")!.Click += async (_, _) =>
            await ExportCsvAsync("portfolio.csv", PortfolioViewModel.BuildPositionsCsv(vm.Positions));
    }

    /// <summary>Native save-file dialog via Avalonia's StorageProvider - same pattern as
    /// UniverseView.axaml.cs's ExportCsvAsync. Best-effort: a cancelled dialog or a write failure
    /// both just no-op rather than surface an error banner.</summary>
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
