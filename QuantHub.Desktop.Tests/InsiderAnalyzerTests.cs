using System.Text.Json;
using QuantHub.Core.Analysis;

namespace QuantHub.Desktop.Tests;

public class InsiderAnalyzerTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Build_ParsesTopInstitutionalHoldersFromOwnershipList()
    {
        var result = Parse("""
        {
            "institutionOwnership": {
                "ownershipList": [
                    { "organization": "Vanguard Group Inc", "pctHeld": { "raw": 0.082 }, "position": { "raw": 123456789 }, "value": { "raw": 9999999 }, "pctChange": { "raw": 0.012 } },
                    { "organization": "", "pctHeld": { "raw": 0.01 } }
                ]
            }
        }
        """);

        var d = InsiderAnalyzer.Build("test", result, "Test Co");

        // The blank-organization row is skipped - a row with no name isn't useful to show.
        Assert.Single(d.TopInstitutionalHolders);
        var top = d.TopInstitutionalHolders[0];
        Assert.Equal("Vanguard Group Inc", top.Organization);
        Assert.Equal(0.082, top.PctHeld);
        Assert.Equal(123456789, top.Position);
        Assert.Equal(0.012, top.PctChange);
    }

    [Fact]
    public void Build_ReturnsEmptyHoldersWhenModuleMissing()
    {
        var result = Parse("{}");
        var d = InsiderAnalyzer.Build("test", result, null);
        Assert.Empty(d.TopInstitutionalHolders);
    }

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
