using QuantHub.Core.Analysis;

namespace QuantHub.Desktop.Tests;

public class InsiderAnalyzerTests
{
    [Theory]
    [InlineData("Sale of common stock", "Sale")]
    [InlineData("Open market sell", "Sale")]
    [InlineData("Purchase of common stock", "Purchase")]
    [InlineData("Bought on open market", "Purchase")]
    [InlineData("Stock gift to family trust", "Gift")]
    [InlineData("Shares donated to charity", "Gift")]
    [InlineData("Option exercise", "Option Exercise")]
    [InlineData("Award grant of restricted stock", "Award/Grant")]
    [InlineData("Something entirely unclassifiable", "Unknown")]
    public void ClassifyTransaction_MatchesExpectedCategory(string text, string expected)
    {
        Assert.Equal(expected, InsiderAnalyzer.ClassifyTransaction(text));
    }

    [Fact]
    public void ClassifyTransaction_SalePrecedesLaterBranchesWhenBothKeywordsPresent()
    {
        // "sale" appears before "option" in the elif chain, so it must win even though
        // both keywords are present in the same text.
        var result = InsiderAnalyzer.ClassifyTransaction("Sale following option exercise");
        Assert.Equal("Sale", result);
    }

    [Fact]
    public void ClassifyTransaction_IsCaseInsensitiveSubstringMatch()
    {
        Assert.Equal("Purchase", InsiderAnalyzer.ClassifyTransaction("BUY TRANSACTION"));
    }
}
