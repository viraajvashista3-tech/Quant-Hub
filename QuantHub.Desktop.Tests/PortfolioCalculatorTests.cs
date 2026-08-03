using QuantHub.Core.Models;
using QuantHub.Core.Portfolio;

namespace QuantHub.Desktop.Tests;

public class PortfolioCalculatorTests
{
    [Fact]
    public void Evaluate_ComputesCostBasisMarketValueAndGainLoss()
    {
        var position = new Position("AAPL", 10, 100, new DateOnly(2026, 1, 1), 400);

        var result = PortfolioCalculator.Evaluate(position, currentPrice: 120, currentBenchmarkPrice: 440);

        Assert.Equal(1000, result.CostBasis);
        Assert.Equal(1200, result.MarketValue);
        Assert.Equal(200, result.GainLossDollar);
        Assert.Equal(20, result.GainLossPct, 4);
        // Own return 20%, benchmark return (440-400)/400*100 = 10% -> excess = 10%.
        Assert.Equal(10, result.ExcessReturnVsBenchmarkPct, 4);
    }

    [Fact]
    public void Evaluate_LosingPosition_NegativeGainLoss()
    {
        var position = new Position("XYZ", 5, 200, new DateOnly(2026, 1, 1), 400);

        var result = PortfolioCalculator.Evaluate(position, currentPrice: 150, currentBenchmarkPrice: 400);

        Assert.Equal(1000, result.CostBasis);
        Assert.Equal(750, result.MarketValue);
        Assert.Equal(-250, result.GainLossDollar);
        Assert.Equal(-25, result.GainLossPct, 4);
    }

    [Fact]
    public void Summarize_EmptyList_ReturnsZeroedSummary()
    {
        var summary = PortfolioCalculator.Summarize([]);

        Assert.Equal(0, summary.TotalCostBasis);
        Assert.Equal(0, summary.TotalMarketValue);
        Assert.Equal(0, summary.WeightedExcessReturnVsBenchmarkPct);
    }

    [Fact]
    public void Summarize_WeightsExcessReturnByCostBasis_NotPlainAverage()
    {
        // Position A: $9,000 cost basis, 0% excess return.
        // Position B: $1,000 cost basis, 40% excess return.
        // Plain average would be 20%; cost-basis-weighted should be much closer to A's 0%.
        var a = PortfolioCalculator.Evaluate(new Position("A", 90, 100, new DateOnly(2026, 1, 1), 100), 100, 100);
        var b = PortfolioCalculator.Evaluate(new Position("B", 10, 100, new DateOnly(2026, 1, 1), 100), 100, 100);
        var bWithExcess = b with { ExcessReturnVsBenchmarkPct = 40 };

        var summary = PortfolioCalculator.Summarize([a, bWithExcess]);

        // (9000*0 + 1000*40) / 10000 = 4
        Assert.Equal(4, summary.WeightedExcessReturnVsBenchmarkPct, 4);
    }

    [Fact]
    public void Summarize_TotalsAcrossMultiplePositions()
    {
        var a = PortfolioCalculator.Evaluate(new Position("A", 10, 100, new DateOnly(2026, 1, 1), 100), 110, 100);
        var b = PortfolioCalculator.Evaluate(new Position("B", 5, 200, new DateOnly(2026, 1, 1), 100), 180, 100);

        var summary = PortfolioCalculator.Summarize([a, b]);

        Assert.Equal(2000, summary.TotalCostBasis); // 1000 + 1000
        Assert.Equal(2000, summary.TotalMarketValue); // 1100 + 900
        Assert.Equal(0, summary.TotalGainLossDollar, 4);
    }

    private static PriceBar Bar(string date, double close) => new()
    {
        Date = date, Open = close, High = close, Low = close, Close = close, Volume = 0
    };

    [Fact]
    public void PriceOnOrBefore_ExactMatch_ReturnsThatBarsClose()
    {
        var bars = new List<PriceBar> { Bar("2026-01-02", 400), Bar("2026-01-05", 405), Bar("2026-01-06", 410) };

        var price = PortfolioCalculator.PriceOnOrBefore(bars, new DateOnly(2026, 1, 5));

        Assert.Equal(405, price);
    }

    [Fact]
    public void PriceOnOrBefore_WeekendOrHoliday_FallsBackToPriorTradingDay()
    {
        var bars = new List<PriceBar> { Bar("2026-01-02", 400), Bar("2026-01-05", 405), Bar("2026-01-06", 410) };

        // 2026-01-03/04 is a weekend with no bar - should resolve to the 01-02 close.
        var price = PortfolioCalculator.PriceOnOrBefore(bars, new DateOnly(2026, 1, 3));

        Assert.Equal(400, price);
    }

    [Fact]
    public void PriceOnOrBefore_DateBeforeAllBars_FallsBackToEarliestBar()
    {
        var bars = new List<PriceBar> { Bar("2026-01-02", 400), Bar("2026-01-05", 405) };

        var price = PortfolioCalculator.PriceOnOrBefore(bars, new DateOnly(2020, 1, 1));

        Assert.Equal(400, price);
    }

    [Fact]
    public void PriceOnOrBefore_EmptyBars_ReturnsNull()
    {
        Assert.Null(PortfolioCalculator.PriceOnOrBefore([], new DateOnly(2026, 1, 1)));
    }
}
