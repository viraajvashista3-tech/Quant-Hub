using QuantHub.Core.Portfolio;
using QuantHub.Desktop.ViewModels.Pages;

namespace QuantHub.Desktop.Tests;

public class PortfolioViewModelTests
{
    private static PositionPerformance Position(string ticker, double shares, double entryPrice, DateOnly entryDate) =>
        PortfolioCalculator.Evaluate(new Position(ticker, shares, entryPrice, entryDate, 400), entryPrice * 1.1, 420);

    [Fact]
    public void BuildPositionsCsv_IncludesHeaderAndOneLinePerRow()
    {
        var csv = PortfolioViewModel.BuildPositionsCsv([Position("AAPL", 10, 100, new DateOnly(2026, 1, 1))]);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal("Ticker,Shares,EntryPrice,EntryDate,CurrentPrice,MarketValue,GainLoss,GainLoss%,ExcessReturnVsSP500%", lines[0]);
        Assert.Contains("AAPL", lines[1]);
        Assert.Contains("2026-01-01", lines[1]);
    }

    [Fact]
    public void BuildPositionsCsv_EmptyList_HeaderOnly()
    {
        var csv = PortfolioViewModel.BuildPositionsCsv([]);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(lines);
    }

    [Fact]
    public void BuildPositionsCsv_MultiplePositions_OneLineEach()
    {
        var csv = PortfolioViewModel.BuildPositionsCsv([
            Position("AAPL", 10, 100, new DateOnly(2026, 1, 1)),
            Position("MSFT", 5, 200, new DateOnly(2026, 2, 1))
        ]);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length); // header + 2 rows
        Assert.Contains("AAPL", lines[1]);
        Assert.Contains("MSFT", lines[2]);
    }
}
