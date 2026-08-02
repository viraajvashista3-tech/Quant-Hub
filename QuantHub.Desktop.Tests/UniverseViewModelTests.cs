using QuantHub.Core.Models;
using QuantHub.Core.Universe;
using QuantHub.Desktop.ViewModels.Pages;

namespace QuantHub.Desktop.Tests;

public class UniverseViewModelTests
{
    [Fact]
    public void ResolveWatchlistTickers_EmptyWatchlist_FallsBackToDefaultTickers()
    {
        var result = UniverseViewModel.ResolveWatchlistTickers([]);

        Assert.Equal(UniverseData.DefaultTickers, result);
    }

    [Fact]
    public void ResolveWatchlistTickers_NonEmptyWatchlist_ReturnsWatchlistUnchanged()
    {
        string[] watchlist = ["TSLA", "NVDA"];

        var result = UniverseViewModel.ResolveWatchlistTickers(watchlist);

        Assert.Equal(watchlist, result);
    }

    private static WatchlistRow Row(string ticker, double score, double? upside, string? rating, int ratingRank) =>
        new(Rank: 0, Ticker: ticker, Name: ticker, Price: 100, ChangePercent: 0,
            QuantScore: score, Signal: Signal.Hold,
            UpsidePotentialPct: upside, ConsensusRating: rating, AnalystRatingRank: ratingRank);

    [Fact]
    public void SortWatchlistRows_QuantScore_OrdersDescendingAndRenumbers()
    {
        IReadOnlyList<WatchlistRow> rows =
        [
            Row("A", 10, null, null, 5),
            Row("B", 40, null, null, 5),
            Row("C", 25, null, null, 5)
        ];

        var sorted = UniverseViewModel.SortWatchlistRows(rows, RankingMetric.QuantScore);

        Assert.Equal(["B", "C", "A"], sorted.Select(r => r.Ticker));
        Assert.Equal([1, 2, 3], sorted.Select(r => r.Rank));
    }

    [Fact]
    public void SortWatchlistRows_UpsidePotential_NullsSortLast()
    {
        IReadOnlyList<WatchlistRow> rows =
        [
            Row("A", 0, 5.0, null, 5),
            Row("B", 0, null, null, 5),
            Row("C", 0, 20.0, null, 5)
        ];

        var sorted = UniverseViewModel.SortWatchlistRows(rows, RankingMetric.UpsidePotential);

        Assert.Equal(["C", "A", "B"], sorted.Select(r => r.Ticker));
    }

    [Fact]
    public void SortWatchlistRows_AnalystRating_OrdersByRankThenTieBreaksByQuantScore()
    {
        IReadOnlyList<WatchlistRow> rows =
        [
            Row("A", 10, null, "Buy", 1),
            Row("B", 30, null, "Buy", 1),
            Row("C", 0, null, "Strong Buy", 0)
        ];

        var sorted = UniverseViewModel.SortWatchlistRows(rows, RankingMetric.AnalystRating);

        // Strong Buy (rank 0) first, then the two Buy (rank 1) rows tie-broken by higher QuantScore first
        Assert.Equal(["C", "B", "A"], sorted.Select(r => r.Ticker));
    }

    [Fact]
    public void BuildWatchlistCsv_IncludesHeaderAndOneLinePerRow()
    {
        IReadOnlyList<WatchlistRow> rows = [Row("AAPL", 42.5, 12.3, "Buy", 1)];

        var csv = UniverseViewModel.BuildWatchlistCsv(rows);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal("Rank,Ticker,Name,Price,Change%,Score,Upside%,Rating,Signal", lines[0]);
        Assert.Contains("AAPL", lines[1]);
        Assert.Contains("42.5", lines[1]);
        Assert.Contains("Buy", lines[1]);
    }

    [Fact]
    public void BuildWatchlistCsv_NameWithComma_IsQuoted()
    {
        var row = new WatchlistRow(1, "BRK.B", "Berkshire Hathaway, Inc.", 500, 0, 10, Signal.Hold, null, null, 5);

        var csv = UniverseViewModel.BuildWatchlistCsv([row]);

        Assert.Contains("\"Berkshire Hathaway, Inc.\"", csv);
    }

    [Fact]
    public void BuildWatchlistCsv_NullUpsideAndRating_RendersAsEmptyField()
    {
        var row = new WatchlistRow(1, "XYZ", "XYZ Corp", 10, 0, 5, Signal.Hold, null, null, 5);

        var csv = UniverseViewModel.BuildWatchlistCsv([row]);
        var dataLine = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)[1];
        var fields = dataLine.Split(',');

        Assert.Equal("", fields[6]); // Upside%
        Assert.Equal("", fields[7]); // Rating
    }

    [Fact]
    public void BuildTop20Csv_IncludesHeaderAndOneLinePerRow()
    {
        IReadOnlyList<Top20Row> rows = [new Top20Row(1, "MSFT", "Microsoft Corp.", 400, 55.2, Signal.Buy, 8.1, "Strong Buy", null)];

        var csv = UniverseViewModel.BuildTop20Csv(rows);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal("Rank,Ticker,Name,Price,Score,Upside%,Rating,Signal", lines[0]);
        Assert.Contains("MSFT", lines[1]);
        Assert.Contains("Strong Buy", lines[1]);
    }
}
