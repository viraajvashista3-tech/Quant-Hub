using QuantHub.Core.Models;
using QuantHub.Desktop.Services;

namespace QuantHub.Desktop.Tests;

public class SessionBriefingServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "QuantHubTests", Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private SessionBriefingService NewService() => new(_dir);

    [Fact]
    public void DiffSignals_TickerWithNoPriorRecord_IsNotReported()
    {
        var previous = new Dictionary<string, Signal>();
        var current = new List<(string, string, Signal)> { ("AAPL", "Apple Inc.", Signal.Buy) };

        var messages = SessionBriefingService.DiffSignals(previous, current);

        Assert.Empty(messages);
    }

    [Fact]
    public void DiffSignals_UnchangedSignal_IsNotReported()
    {
        var previous = new Dictionary<string, Signal> { ["AAPL"] = Signal.Buy };
        var current = new List<(string, string, Signal)> { ("AAPL", "Apple Inc.", Signal.Buy) };

        var messages = SessionBriefingService.DiffSignals(previous, current);

        Assert.Empty(messages);
    }

    [Fact]
    public void DiffSignals_ChangedSignal_IsReportedWithTickerAndNames()
    {
        var previous = new Dictionary<string, Signal> { ["AAPL"] = Signal.Hold };
        var current = new List<(string, string, Signal)> { ("AAPL", "Apple Inc.", Signal.Buy) };

        var messages = SessionBriefingService.DiffSignals(previous, current);

        var message = Assert.Single(messages);
        Assert.Contains("AAPL", message);
        Assert.Contains("Apple Inc.", message);
        Assert.Contains("Hold", message);
        Assert.Contains("Buy", message);
    }

    [Fact]
    public void DiffSignals_MultipleTickers_OnlyReportsTheChangedOnes()
    {
        var previous = new Dictionary<string, Signal> { ["AAPL"] = Signal.Hold, ["MSFT"] = Signal.Buy };
        var current = new List<(string, string, Signal)>
        {
            ("AAPL", "Apple Inc.", Signal.Buy), // changed
            ("MSFT", "Microsoft Corp.", Signal.Buy) // unchanged
        };

        var messages = SessionBriefingService.DiffSignals(previous, current);

        var message = Assert.Single(messages);
        Assert.Contains("AAPL", message);
    }

    [Fact]
    public void RecordAndDiff_FirstEverCall_ReportsNothingButCommitsBaseline()
    {
        var service = NewService();
        var messages = service.RecordAndDiff([("AAPL", "Apple Inc.", Signal.Buy)]);

        Assert.Empty(messages);
    }

    [Fact]
    public void RecordAndDiff_SecondCallWithChange_ReportsIt()
    {
        var service = NewService();
        service.RecordAndDiff([("AAPL", "Apple Inc.", Signal.Hold)]);

        var messages = service.RecordAndDiff([("AAPL", "Apple Inc.", Signal.Buy)]);

        Assert.Single(messages);
    }

    [Fact]
    public void RecordAndDiff_CalledTwiceWithSameData_OnlyReportsChangeOnce()
    {
        var service = NewService();
        service.RecordAndDiff([("AAPL", "Apple Inc.", Signal.Hold)]);
        service.RecordAndDiff([("AAPL", "Apple Inc.", Signal.Buy)]);

        // Third call, still Buy - already committed as the baseline by the second call.
        var messages = service.RecordAndDiff([("AAPL", "Apple Inc.", Signal.Buy)]);

        Assert.Empty(messages);
    }

    [Fact]
    public void NewInstance_ReloadsPersistedBaselineFromDisk()
    {
        NewService().RecordAndDiff([("AAPL", "Apple Inc.", Signal.Hold)]);

        var reloaded = NewService();
        var messages = reloaded.RecordAndDiff([("AAPL", "Apple Inc.", Signal.Buy)]);

        Assert.Single(messages);
    }
}
