using QuantHub.Desktop.ViewModels.Pages;

namespace QuantHub.Desktop.Tests;

public class SettingsViewModelTests
{
    [Fact]
    public void BuildWatchlistExportJson_RoundTripsThroughParse()
    {
        string[] tickers = ["AAPL", "MSFT", "TSLA"];

        var json = SettingsViewModel.BuildWatchlistExportJson(tickers);
        var parsed = SettingsViewModel.ParseWatchlistImportJson(json);

        Assert.Equal(tickers, parsed);
    }

    [Fact]
    public void ParseWatchlistImportJson_MalformedInput_ReturnsEmpty()
    {
        var parsed = SettingsViewModel.ParseWatchlistImportJson("not valid json {{{");

        Assert.Empty(parsed);
    }

    [Fact]
    public void ParseWatchlistImportJson_EmptyArray_ReturnsEmpty()
    {
        var parsed = SettingsViewModel.ParseWatchlistImportJson("[]");

        Assert.Empty(parsed);
    }

    [Fact]
    public void ParseWatchlistImportJson_ValidArray_ReturnsTickers()
    {
        var parsed = SettingsViewModel.ParseWatchlistImportJson("""["NVDA", "AMD"]""");

        Assert.Equal(["NVDA", "AMD"], parsed);
    }
}
