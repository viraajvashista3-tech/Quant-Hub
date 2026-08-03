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
        Assert.Equal(AutoRefreshInterval.Off, service.AutoRefreshInterval);
        Assert.False(service.AlwaysOnTop);
    }

    [Fact]
    public void AlwaysOnTop_PersistsAcrossInstances()
    {
        var service = NewService();
        service.AlwaysOnTop = true;

        var reloaded = NewService();

        Assert.True(reloaded.AlwaysOnTop);
    }

    [Theory]
    [InlineData(AutoRefreshInterval.Off, -1)] // Timeout.InfiniteTimeSpan.Ticks == -1
    [InlineData(AutoRefreshInterval.OneMinute, 60)]
    [InlineData(AutoRefreshInterval.FiveMinutes, 300)]
    [InlineData(AutoRefreshInterval.FifteenMinutes, 900)]
    public void ToTimeSpan_MapsEachIntervalCorrectly(AutoRefreshInterval interval, int expectedSeconds)
    {
        var expected = expectedSeconds < 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(expectedSeconds);

        Assert.Equal(expected, SettingsService.ToTimeSpan(interval));
    }

    [Fact]
    public void AutoRefreshInterval_PersistsAcrossInstances()
    {
        var service = NewService();
        service.AutoRefreshInterval = AutoRefreshInterval.FiveMinutes;

        var reloaded = NewService();

        Assert.Equal(AutoRefreshInterval.FiveMinutes, reloaded.AutoRefreshInterval);
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
