using QuantHub.Core.Models;
using QuantHub.Core.Universe;

namespace QuantHub.Desktop.Tests;

public class UniverseRankingTests
{
    private static TickerRankData Ticker(
        string ticker, double quantScore, double? upside = null, string? rating = null, int ratingRank = 5,
        double? trend = null, double? momentum = null) =>
        new(ticker, ticker, 100, quantScore, Signal.Hold,
            trend, momentum, null, null, null, null, null,
            TargetMean: upside is null ? null : 100 * (1 + upside.Value / 100),
            UpsidePotentialPct: upside, ConsensusRating: rating, AnalystRatingRank: ratingRank);

    private static UniverseSnapshot Snapshot(DateTime ranAtUtc, params TickerRankData[] tickers) =>
        new(ranAtUtc, tickers, []);

    /// <summary>Assigns each ticker a strictly-decreasing QuantScore by its position in the argument
    /// list (1000, 999, 998, ...), so RankByMetric(QuantScore) always ranks them in exactly the order
    /// given - no possible ties, no arithmetic to double-check per test.</summary>
    private static UniverseSnapshot RankedSnapshot(DateTime ranAtUtc, params string[] tickersByRank) =>
        new(ranAtUtc, tickersByRank.Select((t, i) => Ticker(t, 1000 - i)).ToArray(), []);

    // ---------- RankByMetric ----------

    [Fact]
    public void RankByMetric_QuantScore_OrdersDescending()
    {
        var tickers = new[] { Ticker("A", 10), Ticker("B", 40), Ticker("C", 25) };
        var ranked = UniverseRanking.RankByMetric(tickers, RankingMetric.QuantScore);
        Assert.Equal(["B", "C", "A"], ranked.Select(t => t.Ticker));
    }

    [Fact]
    public void RankByMetric_UpsidePotential_ExcludesNulls()
    {
        var tickers = new[] { Ticker("A", 0, upside: 5), Ticker("B", 0, upside: null), Ticker("C", 0, upside: 20) };
        var ranked = UniverseRanking.RankByMetric(tickers, RankingMetric.UpsidePotential);
        Assert.Equal(["C", "A"], ranked.Select(t => t.Ticker));
    }

    [Fact]
    public void RankByMetric_AnalystRating_ExcludesUnratedAndTieBreaksByQuantScore()
    {
        var tickers = new[]
        {
            Ticker("A", 10, rating: "Buy", ratingRank: 1),
            Ticker("B", 30, rating: "Buy", ratingRank: 1),
            Ticker("C", 0, rating: "Strong Buy", ratingRank: 0),
            Ticker("D", 999, rating: "N/A", ratingRank: 5)
        };
        var ranked = UniverseRanking.RankByMetric(tickers, RankingMetric.AnalystRating);
        Assert.Equal(["C", "B", "A"], ranked.Select(t => t.Ticker));
    }

    // ---------- UpdateMonthlyArchive ----------

    [Fact]
    public void UpdateMonthlyArchive_FirstSnapshotEver_ArchivesIt()
    {
        var snap = Snapshot(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        var archive = UniverseRanking.UpdateMonthlyArchive(new Dictionary<string, UniverseSnapshot>(), snap);

        var entry = Assert.Single(archive);
        Assert.Equal("2026-01", entry.Key);
        Assert.Same(snap, entry.Value);
    }

    [Fact]
    public void UpdateMonthlyArchive_SecondSnapshotSameMonth_LeavesArchiveUnchanged()
    {
        var first = Snapshot(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        var archive = UniverseRanking.UpdateMonthlyArchive(new Dictionary<string, UniverseSnapshot>(), first);

        var second = Snapshot(new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc));
        var updated = UniverseRanking.UpdateMonthlyArchive(archive, second);

        var entry = Assert.Single(updated);
        Assert.Equal("2026-01", entry.Key);
        Assert.Same(first, entry.Value); // still the first-of-month snapshot, not overwritten
    }

    [Fact]
    public void UpdateMonthlyArchive_NewMonth_GrowsArchiveToTwoEntries()
    {
        var jan = Snapshot(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        var archive = UniverseRanking.UpdateMonthlyArchive(new Dictionary<string, UniverseSnapshot>(), jan);

        var feb = Snapshot(new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc));
        var updated = UniverseRanking.UpdateMonthlyArchive(archive, feb);

        Assert.Equal(2, updated.Count);
        Assert.Same(jan, updated["2026-01"]);
        Assert.Same(feb, updated["2026-02"]);
    }

    [Fact]
    public void UpdateMonthlyArchive_ThirdDistinctMonth_PrunesOldestKeepingTwo()
    {
        var archive = UniverseRanking.UpdateMonthlyArchive(
            new Dictionary<string, UniverseSnapshot>(), Snapshot(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)));
        archive = UniverseRanking.UpdateMonthlyArchive(
            archive, Snapshot(new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc)));
        var mar = Snapshot(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        archive = UniverseRanking.UpdateMonthlyArchive(archive, mar);

        Assert.Equal(2, archive.Count);
        Assert.False(archive.ContainsKey("2026-01"));
        Assert.True(archive.ContainsKey("2026-02"));
        Assert.Same(mar, archive["2026-03"]);
    }

    // ---------- PreviousMonthSnapshot ----------

    [Fact]
    public void PreviousMonthSnapshot_EmptyArchive_ReturnsNull()
    {
        var result = UniverseRanking.PreviousMonthSnapshot(new Dictionary<string, UniverseSnapshot>(), new DateTime(2026, 1, 20));
        Assert.Null(result);
    }

    [Fact]
    public void PreviousMonthSnapshot_SingleMonthArchive_ReturnsNull()
    {
        var jan = Snapshot(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        var archive = new Dictionary<string, UniverseSnapshot> { ["2026-01"] = jan };
        var result = UniverseRanking.PreviousMonthSnapshot(archive, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc));
        Assert.Null(result);
    }

    [Fact]
    public void PreviousMonthSnapshot_TwoMonthArchive_ReturnsNonCurrentOne()
    {
        var jan = Snapshot(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        var feb = Snapshot(new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc));
        var archive = new Dictionary<string, UniverseSnapshot> { ["2026-01"] = jan, ["2026-02"] = feb };

        var result = UniverseRanking.PreviousMonthSnapshot(archive, new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc));

        Assert.Same(jan, result);
    }

    // ---------- ExplainTopNChanges ----------

    [Fact]
    public void ExplainTopNChanges_NullPrevious_ReturnsEmpty()
    {
        var current = Snapshot(DateTime.UtcNow, Ticker("A", 50));
        var result = UniverseRanking.ExplainTopNChanges(null, current, RankingMetric.QuantScore, n: 20);
        Assert.Empty(result);
    }

    [Fact]
    public void ExplainTopNChanges_TickerEntersTopN_Explained()
    {
        // Previous: 20 filler tickers rank #1-20, "NEW" rank #21 (just outside). Current: NEW jumps to #1.
        var fillers = Enumerable.Range(1, 20).Select(i => $"T{i}").ToArray();
        var previous = RankedSnapshot(new DateTime(2026, 1, 15), [.. fillers, "NEW"]);
        var current = RankedSnapshot(new DateTime(2026, 2, 3), ["NEW", .. fillers]);

        var changes = UniverseRanking.ExplainTopNChanges(previous, current, RankingMetric.QuantScore, n: 20);

        var change = Assert.Single(changes, c => c.Ticker == "NEW");
        Assert.Equal(21, change.PreviousRank);
        Assert.Equal(1, change.CurrentRank);
        Assert.Contains("entered the Top 20", change.Explanation);
        Assert.Contains("up from #21", change.Explanation);
    }

    [Fact]
    public void ExplainTopNChanges_TickerLeavesTopN_Explained()
    {
        // Previous: OUT ranked #20 (just inside). Current: OUT falls to #21 (just outside).
        var fillers19 = Enumerable.Range(1, 19).Select(i => $"T{i}").ToArray();
        var fillers20 = Enumerable.Range(1, 20).Select(i => $"T{i}").ToArray();
        var previous = RankedSnapshot(new DateTime(2026, 1, 15), [.. fillers19, "OUT"]);
        var current = RankedSnapshot(new DateTime(2026, 2, 3), [.. fillers20, "OUT"]);

        var changes = UniverseRanking.ExplainTopNChanges(previous, current, RankingMetric.QuantScore, n: 20);

        var change = Assert.Single(changes, c => c.Ticker == "OUT");
        Assert.Equal(20, change.PreviousRank);
        Assert.Equal(21, change.CurrentRank);
        Assert.Contains("dropped out of the Top 20", change.Explanation);
    }

    [Fact]
    public void ExplainTopNChanges_MovedEnoughWithinTopN_Explained()
    {
        // MOVER goes from #10 to #2 (an 8-spot move, well above the default threshold of 3).
        var before = new List<string> { "T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "T9", "MOVER" };
        before.AddRange(Enumerable.Range(10, 10).Select(i => $"T{i}"));
        var after = new List<string> { "T1", "MOVER" };
        after.AddRange(Enumerable.Range(2, 18).Select(i => $"T{i}"));

        var previous = RankedSnapshot(new DateTime(2026, 1, 15), before.ToArray());
        var current = RankedSnapshot(new DateTime(2026, 2, 3), after.ToArray());

        var changes = UniverseRanking.ExplainTopNChanges(previous, current, RankingMetric.QuantScore, n: 20);

        var change = Assert.Single(changes, c => c.Ticker == "MOVER");
        Assert.Equal(10, change.PreviousRank);
        Assert.Equal(2, change.CurrentRank);
        Assert.Contains("climbed from #10 to #2", change.Explanation);
    }

    [Fact]
    public void ExplainTopNChanges_MovedTooLittleWithinTopN_NotExplained()
    {
        // STABLE moves from #5 to #6 - a 1-spot move, below the default threshold of 3.
        var before = new List<string> { "T1", "T2", "T3", "T4", "STABLE" };
        before.AddRange(Enumerable.Range(5, 15).Select(i => $"T{i}"));
        var after = new List<string> { "T1", "T2", "T3", "T4", "T5", "STABLE" };
        after.AddRange(Enumerable.Range(6, 14).Select(i => $"T{i}"));

        var previous = RankedSnapshot(new DateTime(2026, 1, 15), before.ToArray());
        var current = RankedSnapshot(new DateTime(2026, 2, 3), after.ToArray());

        var changes = UniverseRanking.ExplainTopNChanges(previous, current, RankingMetric.QuantScore, n: 20);

        Assert.DoesNotContain(changes, c => c.Ticker == "STABLE");
    }

    [Fact]
    public void ExplainTopNChanges_QuantScoreDetail_NamesTopDrivers()
    {
        var previous = Snapshot(new DateTime(2026, 1, 15),
            Ticker("A", 20, trend: 0.1, momentum: 0.1));
        var current = Snapshot(new DateTime(2026, 2, 3),
            Ticker("A", 40, trend: 0.9, momentum: 0.1));

        var changes = UniverseRanking.ExplainTopNChanges(previous, current, RankingMetric.QuantScore, n: 20, minRankMoveToExplain: 0);

        var change = Assert.Single(changes, c => c.Ticker == "A");
        Assert.Contains("Trend", change.Explanation);
        Assert.Contains("20", change.Explanation);
        Assert.Contains("40", change.Explanation);
    }

    [Fact]
    public void ExplainTopNChanges_AnalystRatingDetail_ReportsRatingChange()
    {
        var previous = Snapshot(new DateTime(2026, 1, 15), Ticker("A", 0, rating: "Hold", ratingRank: 2));
        var current = Snapshot(new DateTime(2026, 2, 3), Ticker("A", 0, rating: "Strong Buy", ratingRank: 0));

        var changes = UniverseRanking.ExplainTopNChanges(previous, current, RankingMetric.AnalystRating, n: 20, minRankMoveToExplain: 0);

        var change = Assert.Single(changes, c => c.Ticker == "A");
        Assert.Contains("consensus rating changed from Hold to Strong Buy", change.Explanation);
    }
}
