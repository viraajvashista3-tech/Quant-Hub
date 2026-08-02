using QuantHub.Desktop.Services;

namespace QuantHub.Desktop.Tests;

public class WatchlistServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "QuantHubTests", Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private WatchlistService NewService() => new(_dir);

    [Fact]
    public void Add_NewTicker_IsStoredUppercased()
    {
        var service = NewService();
        service.Add("aapl");

        Assert.Equal(["AAPL"], service.Tickers);
    }

    [Fact]
    public void Add_DuplicateTicker_IsIgnored()
    {
        var service = NewService();
        service.Add("AAPL");
        service.Add("aapl");

        Assert.Single(service.Tickers);
    }

    [Fact]
    public void Add_BlankTicker_IsIgnored()
    {
        var service = NewService();
        service.Add("   ");

        Assert.Empty(service.Tickers);
    }

    [Fact]
    public void Remove_ExistingTicker_RemovesItRegardlessOfCase()
    {
        var service = NewService();
        service.Add("MSFT");
        service.Remove("msft");

        Assert.Empty(service.Tickers);
    }

    [Fact]
    public void Remove_UnknownTicker_IsNoOp()
    {
        var service = NewService();

        service.Remove("MSFT");

        Assert.Empty(service.Tickers);
    }

    [Fact]
    public void Add_RaisesChangedEvent()
    {
        var service = NewService();
        var raised = false;
        service.Changed += (_, _) => raised = true;

        service.Add("NVDA");

        Assert.True(raised);
    }

    [Fact]
    public void Remove_WhenNothingRemoved_DoesNotRaiseChangedEvent()
    {
        var service = NewService();
        var raised = false;
        service.Changed += (_, _) => raised = true;

        service.Remove("NVDA");

        Assert.False(raised);
    }

    [Fact]
    public void NewInstance_ReloadsPersistedTickersFromDisk()
    {
        NewService().Add("TSLA");

        var reloaded = NewService();

        Assert.Equal(["TSLA"], reloaded.Tickers);
    }
}
