using QuantHub.Desktop.Services;
using QuantHub.Desktop.ViewModels;

namespace QuantHub.Desktop.Tests;

public class AppStateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "QuantHubTests", Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private SettingsService NewSettings() => new(_dir);

    [Fact]
    public void NewInstance_WithNoPersistedTicker_DefaultsToAapl()
    {
        var appState = new AppState(NewSettings());

        Assert.Equal("AAPL", appState.ActiveTicker);
    }

    [Fact]
    public void NewInstance_InitializesFromPersistedLastTicker()
    {
        var settings = NewSettings();
        settings.LastTicker = "TSLA";
        settings.Save();

        var appState = new AppState(NewSettings());

        Assert.Equal("TSLA", appState.ActiveTicker);
    }

    [Fact]
    public void ChangingActiveTicker_PersistsToSettings()
    {
        var settings = NewSettings();
        var appState = new AppState(settings);

        appState.ActiveTicker = "NVDA";

        Assert.Equal("NVDA", settings.LastTicker);
        var reloaded = NewSettings();
        Assert.Equal("NVDA", reloaded.LastTicker);
    }
}
