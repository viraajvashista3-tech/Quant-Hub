using QuantHub.Desktop.Services;

namespace QuantHub.Desktop.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "QuantHubTests", Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private SettingsService NewService() => new(_dir);

    [Fact]
    public void NewInstance_WithNoPersistedFile_StartsAtDefaults()
    {
        var service = NewService();

        Assert.Equal(ViewMode.Intermediate, service.ViewMode);
        Assert.Equal(AppTheme.Dark, service.Theme);
        Assert.Equal("cyan", service.AccentName);
        Assert.Equal("AAPL", service.LastTicker);
        Assert.Equal(StartupPage.LastViewed, service.StartupPage);
        Assert.Equal("Terminal", service.LastViewedNavTag);
    }

    [Fact]
    public void LastTicker_PersistsAcrossInstances()
    {
        var service = NewService();
        service.LastTicker = "TSLA";
        service.Save();

        var reloaded = NewService();

        Assert.Equal("TSLA", reloaded.LastTicker);
    }

    [Fact]
    public void StartupPageAndLastViewedNavTag_PersistAcrossInstances()
    {
        var service = NewService();
        service.StartupPage = StartupPage.Universe;
        service.LastViewedNavTag = "Peers";
        service.Save();

        var reloaded = NewService();

        Assert.Equal(StartupPage.Universe, reloaded.StartupPage);
        Assert.Equal("Peers", reloaded.LastViewedNavTag);
    }

    [Fact]
    public void ViewModeAndAccent_StillPersistAlongsideNewFields()
    {
        // Theme is deliberately not exercised here - its setter calls ApplyTheme(), which touches
        // Avalonia's live Application.Current and isn't meaningful outside a running app.
        var service = NewService();
        service.ViewMode = ViewMode.Pro;
        service.AccentName = "violet";
        service.Save();

        var reloaded = NewService();

        Assert.Equal(ViewMode.Pro, reloaded.ViewMode);
        Assert.Equal("violet", reloaded.AccentName);
    }
}
