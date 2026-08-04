using System.Net.Http;
using QuantHub.Core.Alerts;
using QuantHub.Core.Sentiment;
using QuantHub.Core.Services;
using QuantHub.Core.Yahoo;
using QuantHub.Desktop.Services;

namespace QuantHub.Desktop.Tests;

public class AlertServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "QuantHubTests", Guid.NewGuid().ToString());
    private readonly HttpClient _http = new();
    private readonly StockAnalysisService _stockAnalysis;

    public AlertServiceTests()
    {
        _stockAnalysis = new StockAnalysisService(new YahooFinanceClient(_http), new SentimentService(_http));
    }

    public void Dispose()
    {
        _http.Dispose();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private AlertService NewService() => new(_stockAnalysis, _dir);

    [Fact]
    public void NewInstance_WithNoPersistedFile_StartsEmpty()
    {
        Assert.Empty(NewService().Alerts);
    }

    [Fact]
    public void AddAlert_AppearsInActiveAlertsForThatTicker()
    {
        var service = NewService();

        service.AddAlert("aapl", AlertDirection.Above, 150);

        var active = service.ActiveAlertsFor("AAPL");
        var alert = Assert.Single(active);
        Assert.Equal("AAPL", alert.Ticker); // lowercase input normalized to uppercase
        Assert.Equal(AlertDirection.Above, alert.Direction);
        Assert.Equal(150, alert.TargetPrice);
        Assert.Null(alert.TriggeredAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AddAlert_NonPositiveTargetPrice_DoesNotAdd(double targetPrice)
    {
        var service = NewService();

        service.AddAlert("AAPL", AlertDirection.Above, targetPrice);

        Assert.Empty(service.Alerts);
    }

    [Fact]
    public void AddAlert_PersistsAcrossInstances()
    {
        NewService().AddAlert("AAPL", AlertDirection.Below, 100);

        var reloaded = NewService();

        Assert.Single(reloaded.Alerts);
    }

    [Fact]
    public void RemoveAlert_RemovesById()
    {
        var service = NewService();
        service.AddAlert("AAPL", AlertDirection.Above, 150);
        var id = service.Alerts[0].Id;

        service.RemoveAlert(id);

        Assert.Empty(service.Alerts);
    }

    [Fact]
    public void RemoveAlert_UnknownId_NoOps()
    {
        var service = NewService();
        service.AddAlert("AAPL", AlertDirection.Above, 150);

        service.RemoveAlert(Guid.NewGuid());

        Assert.Single(service.Alerts);
    }

    [Fact]
    public void CheckTicker_PriceCrossesThreshold_MarksTriggeredAndRaisesEvent()
    {
        var service = NewService();
        service.AddAlert("AAPL", AlertDirection.Above, 150);
        IReadOnlyList<PriceAlert>? raised = null;
        service.Triggered += (_, alerts) => raised = alerts;

        service.CheckTicker("AAPL", 151);

        Assert.NotNull(raised);
        var triggered = Assert.Single(raised);
        Assert.NotNull(triggered.TriggeredAtUtc);
        Assert.Equal(151, triggered.TriggeredAtPrice);
        Assert.Empty(service.ActiveAlertsFor("AAPL"));
    }

    [Fact]
    public void CheckTicker_PriceDoesNotCrossThreshold_StaysActiveAndNoEvent()
    {
        var service = NewService();
        service.AddAlert("AAPL", AlertDirection.Above, 150);
        var raised = false;
        service.Triggered += (_, _) => raised = true;

        service.CheckTicker("AAPL", 149);

        Assert.False(raised);
        Assert.Single(service.ActiveAlertsFor("AAPL"));
    }

    [Fact]
    public void CheckTicker_AlreadyTriggered_NotRaisedAgainOnSubsequentCheck()
    {
        var service = NewService();
        service.AddAlert("AAPL", AlertDirection.Above, 150);
        service.CheckTicker("AAPL", 151); // first crossing - triggers
        var raisedAgain = false;
        service.Triggered += (_, _) => raisedAgain = true;

        service.CheckTicker("AAPL", 160); // still above target, but already triggered

        Assert.False(raisedAgain);
    }

    [Fact]
    public void CheckTicker_DifferentTicker_Ignored()
    {
        var service = NewService();
        service.AddAlert("AAPL", AlertDirection.Above, 150);
        var raised = false;
        service.Triggered += (_, _) => raised = true;

        service.CheckTicker("MSFT", 500);

        Assert.False(raised);
        Assert.Single(service.ActiveAlertsFor("AAPL"));
    }

    [Fact]
    public void CheckTicker_TriggerPersistsAcrossInstances()
    {
        var service = NewService();
        service.AddAlert("AAPL", AlertDirection.Above, 150);
        service.CheckTicker("AAPL", 151);

        var reloaded = NewService();

        Assert.Empty(reloaded.ActiveAlertsFor("AAPL"));
        Assert.Single(reloaded.Alerts);
        Assert.NotNull(reloaded.Alerts[0].TriggeredAtUtc);
    }
}
