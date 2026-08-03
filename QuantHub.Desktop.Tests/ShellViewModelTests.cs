using QuantHub.Desktop.Services;
using QuantHub.Desktop.ViewModels;

namespace QuantHub.Desktop.Tests;

public class ShellViewModelTests
{
    [Theory]
    [InlineData(StartupPage.Terminal, "Peers", "Terminal")]
    [InlineData(StartupPage.Universe, "Peers", "Universe")]
    [InlineData(StartupPage.TrackRecord, "Peers", "TrackRecord")]
    [InlineData(StartupPage.LastViewed, "Peers", "Peers")]
    [InlineData(StartupPage.LastViewed, "Insider", "Insider")]
    public void ResolveStartupNavTag_MapsSettingToExpectedTag(StartupPage startupPage, string lastViewed, string expected)
    {
        Assert.Equal(expected, ShellViewModel.ResolveStartupNavTag(startupPage, lastViewed));
    }
}
