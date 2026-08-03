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

    [Fact]
    public void VersionText_MatchesTheDesktopAssemblysOwnVersion()
    {
        // Reads the version from QuantHub.Desktop's own assembly metadata (typeof(...).Assembly,
        // not GetExecutingAssembly() - this test project is a separate assembly) rather than
        // hardcoding an expected string, so it stays correct across version bumps.
        var expectedVersion = typeof(SettingsViewModel).Assembly.GetName().Version!.ToString(3);

        Assert.Equal($"Quant Terminal v{expectedVersion}", SettingsViewModel.VersionText);
    }
}
