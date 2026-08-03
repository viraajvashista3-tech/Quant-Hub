using System.Net.Http;
using QuantHub.Desktop.Services;

namespace QuantHub.Desktop.Tests;

public class UpdateCheckServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "QuantHubTests", Guid.NewGuid().ToString());
    private readonly HttpClient _http = new();

    public void Dispose()
    {
        _http.Dispose();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private UpdateCheckService NewService() => new(_http, _dir);

    [Theory]
    [InlineData("1.0.0", "v1.1.0", true)]
    [InlineData("1.0.0", "v2.0.0", true)]
    [InlineData("1.2.0", "v1.10.0", true)] // numeric compare, not lexicographic ("10" > "2")
    [InlineData("1.0.0", "v1.0.0", false)]
    [InlineData("1.5.0", "v1.0.0", false)]
    [InlineData("1.0.0.0", "v1.0.1", true)] // 4-part assembly version vs 3-part tag
    [InlineData("1.0.0", "not-a-version", false)]
    [InlineData("1.0.0", "v1.0.0-beta", false)] // pre-release suffixes never parse as an update
    public void IsNewerVersion_ComparesNumericallyAndDegradesSafely(string current, string latestTag, bool expected)
    {
        Assert.Equal(expected, UpdateCheckService.IsNewerVersion(current, latestTag));
    }

    [Fact]
    public void NewInstance_WithNoPersistedFile_HasNoCurrentResult()
    {
        var service = NewService();

        Assert.Null(service.Current);
    }

    [Fact]
    public void RecordCheckResult_PersistsAndComputesUpdateAvailability()
    {
        var service = NewService();
        var runningVersion = typeof(UpdateCheckService).Assembly.GetName().Version!.ToString(3);
        var newerTag = $"v{Version.Parse(runningVersion).Major + 1}.0.0";

        service.RecordCheckResult(newerTag, "https://github.com/example/repo/releases/tag/" + newerTag);

        Assert.NotNull(service.Current);
        Assert.True(service.Current!.IsUpdateAvailable);
        Assert.Equal(newerTag.TrimStart('v'), service.Current.LatestVersion);
    }

    [Fact]
    public void RecordCheckResult_SameVersion_NotAnUpdate()
    {
        var service = NewService();
        var runningVersion = typeof(UpdateCheckService).Assembly.GetName().Version!.ToString(3);

        service.RecordCheckResult($"v{runningVersion}", "https://github.com/example/repo/releases/tag/v" + runningVersion);

        Assert.False(service.Current!.IsUpdateAvailable);
    }

    [Fact]
    public void RecordCheckResult_PersistsAcrossInstances()
    {
        NewService().RecordCheckResult("v99.0.0", "https://github.com/example/repo/releases/tag/v99.0.0");

        var reloaded = NewService();

        Assert.NotNull(reloaded.Current);
        Assert.True(reloaded.Current!.IsUpdateAvailable);
        Assert.Equal("99.0.0", reloaded.Current.LatestVersion);
    }

    [Fact]
    public void RecordCheckResult_RaisesUpdatedEvent()
    {
        var service = NewService();
        var raised = false;
        service.Updated += (_, _) => raised = true;

        service.RecordCheckResult("v99.0.0", "https://github.com/example/repo/releases/tag/v99.0.0");

        Assert.True(raised);
    }
}
