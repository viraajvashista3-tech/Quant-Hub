using QuantHub.Core.Analysis;
using QuantHub.Core.Models;

namespace QuantHub.Desktop.Tests;

public class PeersAnalyzerTests
{
    [Theory]
    [InlineData("hello world", "Hello world")]
    [InlineData("HELLO WORLD", "Hello world")]
    [InlineData("Apple Inc. commands a premium valuation vs its Technology peers", "Apple inc. commands a premium valuation vs its technology peers")]
    [InlineData("", "")]
    public void PythonCapitalize_UppercasesFirstCharOnlyAndLowercasesTheRest(string input, string expected)
    {
        // Deliberately mangles embedded proper nouns / abbreviations - this is a quirk of the
        // original Python's str.capitalize() that must be preserved, not "fixed".
        Assert.Equal(expected, PeersAnalyzer.PythonCapitalize(input));
    }

    [Fact]
    public void GeneratePeersSummary_PremiumValuationBranch_ExactSentenceAndCapitalizeQuirk()
    {
        var peers = new List<PeerStock>
        {
            new() { Ticker = "TEST", Name = "Test Co", Pe = 20.0 },
            new() { Ticker = "PEER1", Pe = 10.0 },
            new() { Ticker = "PEER2", Pe = 10.0 }
        };

        var summary = PeersAnalyzer.GeneratePeersSummary("TEST", peers, "Tech");

        // diffPct = (20-10)/10*100 = 100% > 10 -> premium branch.
        // Raw sentence: "Test Co commands a premium valuation vs its Tech peers (P/E 20.0x vs sector median 10.0x)"
        // After PythonCapitalize (first char upper, rest lower) + trailing period:
        Assert.Equal(
            "Test co commands a premium valuation vs its tech peers (p/e 20.0x vs sector median 10.0x).",
            summary);
    }

    [Fact]
    public void GeneratePeersSummary_InLineValuation_NoOtherMetrics()
    {
        var peers = new List<PeerStock>
        {
            new() { Ticker = "TEST", Name = "Test Co", Pe = 10.5 },
            new() { Ticker = "PEER1", Pe = 10.0 }
        };

        var summary = PeersAnalyzer.GeneratePeersSummary("TEST", peers, "Tech");

        // diffPct = (10.5-10)/10*100 = 5% <= 10 -> "trades in line" branch.
        Assert.Equal("Test co trades in line with tech peers on valuation (p/e 10.5x).", summary);
    }

    [Fact]
    public void GeneratePeersSummary_NoMetricsAvailable_FallsBackToGenericSentence()
    {
        var peers = new List<PeerStock>
        {
            new() { Ticker = "TEST", Name = "Test Co" },
            new() { Ticker = "PEER1" }
        };

        var summary = PeersAnalyzer.GeneratePeersSummary("TEST", peers, "Tech");
        Assert.Equal("Test Co shows broadly similar characteristics to its Tech sector peers.", summary);
    }

    [Fact]
    public void GeneratePeersSummary_SubjectNotFound_ReturnsNull()
    {
        var peers = new List<PeerStock> { new() { Ticker = "PEER1", Pe = 10.0 } };
        var summary = PeersAnalyzer.GeneratePeersSummary("TEST", peers, "Tech");
        Assert.Null(summary);
    }

    [Fact]
    public void GeneratePeersSummary_BetaAndRoeHaveNoInLineSentence()
    {
        // Beta/ROE within threshold ranges should produce no sentence fragment at all (unlike P/E
        // and margins, which both have an explicit "in line" fallback branch).
        var peers = new List<PeerStock>
        {
            new() { Ticker = "TEST", Name = "Test Co", Beta = 1.0, ReturnOnEquity = 0.10 },
            new() { Ticker = "PEER1", Beta = 1.0, ReturnOnEquity = 0.10 }
        };

        var summary = PeersAnalyzer.GeneratePeersSummary("TEST", peers, "Tech");
        Assert.Equal("Test Co shows broadly similar characteristics to its Tech sector peers.", summary);
    }
}
