using System.Net.Http;
using QuantHub.Core.Sentiment;
using QuantHub.Core.Services;
using QuantHub.Core.Yahoo;
using QuantHub.Desktop.Services;

namespace QuantHub.Desktop.Tests;

public class PortfolioServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "QuantHubTests", Guid.NewGuid().ToString());
    private readonly HttpClient _http = new();
    private readonly StockAnalysisService _stockAnalysis;

    public PortfolioServiceTests()
    {
        _stockAnalysis = new StockAnalysisService(new YahooFinanceClient(_http), new SentimentService(_http));
    }

    public void Dispose()
    {
        _http.Dispose();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private PortfolioService NewService() => new(_stockAnalysis, _dir);

    private void SeedPositionsFile(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "portfolio.json"), json);
    }

    [Fact]
    public void NewInstance_WithNoPersistedFile_StartsEmpty()
    {
        Assert.Empty(NewService().Positions);
    }

    // AddPositionAsync/EvaluateAllAsync both need a live Yahoo fetch (for the entry-date benchmark
    // price and current prices respectively) and are deliberately not exercised here - matching this
    // codebase's convention of not unit-testing the network-touching half of these services (see
    // YahooFinanceClient/SentimentService, neither of which have test files). PortfolioCalculatorTests
    // covers the pure math/date-lookup logic those methods delegate to. These tests seed
    // portfolio.json directly to exercise Load/RemovePosition without any network dependency.

    [Fact]
    public void Positions_LoadsFromPersistedFile()
    {
        SeedPositionsFile(
            """[{ "Ticker": "TSLA", "Shares": 3, "EntryPrice": 250, "EntryDate": "2026-03-01", "EntryBenchmarkPrice": 420 }]""");

        var position = Assert.Single(NewService().Positions);

        Assert.Equal("TSLA", position.Ticker);
        Assert.Equal(3, position.Shares);
        Assert.Equal(new DateOnly(2026, 3, 1), position.EntryDate);
    }

    [Fact]
    public void RemovePosition_MatchesOnTickerAndEntryDate_NotTickerAlone()
    {
        // Two lots of the same ticker at different entry dates - removing one must not remove both.
        SeedPositionsFile("""
        [
          { "Ticker": "AAPL", "Shares": 10, "EntryPrice": 100, "EntryDate": "2026-01-01", "EntryBenchmarkPrice": 400 },
          { "Ticker": "AAPL", "Shares": 5, "EntryPrice": 120, "EntryDate": "2026-02-01", "EntryBenchmarkPrice": 410 }
        ]
        """);
        var service = NewService();

        service.RemovePosition("AAPL", new DateOnly(2026, 1, 1));

        var remaining = Assert.Single(service.Positions);
        Assert.Equal(new DateOnly(2026, 2, 1), remaining.EntryDate);
    }

    [Fact]
    public void RemovePosition_NoMatch_LeavesPositionsUnchanged()
    {
        SeedPositionsFile(
            """[{ "Ticker": "AAPL", "Shares": 10, "EntryPrice": 100, "EntryDate": "2026-01-01", "EntryBenchmarkPrice": 400 }]""");
        var service = NewService();

        service.RemovePosition("MSFT", new DateOnly(2026, 1, 1));

        Assert.Single(service.Positions);
    }

    [Fact]
    public void RemovePosition_PersistsAcrossInstances()
    {
        SeedPositionsFile(
            """[{ "Ticker": "AAPL", "Shares": 10, "EntryPrice": 100, "EntryDate": "2026-01-01", "EntryBenchmarkPrice": 400 }]""");
        NewService().RemovePosition("AAPL", new DateOnly(2026, 1, 1));

        Assert.Empty(NewService().Positions);
    }

    [Fact]
    public void RemovePosition_RaisesChangedEvent()
    {
        SeedPositionsFile(
            """[{ "Ticker": "AAPL", "Shares": 10, "EntryPrice": 100, "EntryDate": "2026-01-01", "EntryBenchmarkPrice": 400 }]""");
        var service = NewService();
        var raised = false;
        service.Changed += (_, _) => raised = true;

        service.RemovePosition("AAPL", new DateOnly(2026, 1, 1));

        Assert.True(raised);
    }
}
