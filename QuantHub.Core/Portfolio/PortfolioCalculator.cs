using QuantHub.Core.Backtesting;
using QuantHub.Core.Models;

namespace QuantHub.Core.Portfolio;

/// <summary>Pure P&amp;L math for user-entered positions - deliberately separate from
/// PredictionLog/BacktestEngine, which grade the Quant Score's own calls. This answers a different
/// question ("how is my actual portfolio doing") using the same honest, benchmark-relative
/// methodology (BacktestEngine.ExcessReturnPct) so the two read the same way everywhere in the app.</summary>
public static class PortfolioCalculator
{
    public static PositionPerformance Evaluate(Position position, double currentPrice, double currentBenchmarkPrice)
    {
        var costBasis = position.Shares * position.EntryPrice;
        var marketValue = position.Shares * currentPrice;
        var gainLossPct = position.EntryPrice != 0 ? (currentPrice - position.EntryPrice) / position.EntryPrice * 100 : 0;
        var excessReturn = BacktestEngine.ExcessReturnPct(
            position.EntryPrice, currentPrice, position.EntryBenchmarkPrice, currentBenchmarkPrice);

        return new PositionPerformance(
            position.Ticker, position.Shares, position.EntryPrice, position.EntryDate,
            currentPrice, costBasis, marketValue,
            marketValue - costBasis, gainLossPct, excessReturn);
    }

    /// <summary>Excess return is weighted by cost basis, not a plain average across positions - a
    /// $10,000 position moving 5% should count for more than a $10 position moving 50%.</summary>
    public static PortfolioSummary Summarize(IReadOnlyList<PositionPerformance> positions)
    {
        if (positions.Count == 0) return new PortfolioSummary(0, 0, 0, 0, 0);

        var totalCostBasis = positions.Sum(p => p.CostBasis);
        var totalMarketValue = positions.Sum(p => p.MarketValue);
        var totalGainLossDollar = totalMarketValue - totalCostBasis;
        var totalGainLossPct = totalCostBasis != 0 ? totalGainLossDollar / totalCostBasis * 100 : 0;
        var weightedExcessReturn = totalCostBasis != 0
            ? positions.Sum(p => p.ExcessReturnVsBenchmarkPct * p.CostBasis) / totalCostBasis
            : 0;

        return new PortfolioSummary(totalCostBasis, totalMarketValue, totalGainLossDollar, totalGainLossPct, weightedExcessReturn);
    }

    /// <summary>Finds the benchmark's closing price on the trading day on/before the given date - for
    /// computing what a position's entry-time benchmark price was when the user only supplies a
    /// calendar date (not necessarily an exact trading day). Falls back to the earliest available bar
    /// if the date predates the whole fetched history window, rather than returning null - an
    /// approximate benchmark is more useful here than refusing to log the position at all.</summary>
    public static double? PriceOnOrBefore(IReadOnlyList<PriceBar> bars, DateOnly date)
    {
        if (bars.Count == 0) return null;
        var target = date.ToString("yyyy-MM-dd");
        // Bars are chronologically ordered (see YahooFinanceClient/StockAnalysisService) and dated
        // "yyyy-MM-dd", so ordinal string comparison sorts identically to date order.
        var match = bars.LastOrDefault(b => string.CompareOrdinal(b.Date, target) <= 0);
        return match?.Close ?? bars[0].Close;
    }
}
