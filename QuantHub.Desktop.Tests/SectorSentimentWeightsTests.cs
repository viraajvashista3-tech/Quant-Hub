using QuantHub.Core.Analysis;

namespace QuantHub.Desktop.Tests;

public class SectorSentimentWeightsTests
{
    [Theory]
    [InlineData("Technology", 1.5)]
    [InlineData("Communication Services", 1.3)]
    [InlineData("Financial Services", 1.0)]
    [InlineData("Consumer Defensive", 0.7)]
    [InlineData("Utilities", 0.6)]
    public void ForSector_KnownSectors_ReturnExpectedMultiplier(string sector, double expected)
    {
        Assert.Equal(expected, SectorSentimentWeights.ForSector(sector));
    }

    [Fact]
    public void ForSector_UnknownSector_FallsBackToDefault()
    {
        Assert.Equal(SectorSentimentWeights.DefaultMultiplier, SectorSentimentWeights.ForSector("Some Unrecognized Sector"));
    }

    [Fact]
    public void ForSector_NullSector_FallsBackToDefault()
    {
        Assert.Equal(SectorSentimentWeights.DefaultMultiplier, SectorSentimentWeights.ForSector(null));
    }

    [Fact]
    public void ForSector_TechAndAiAdjacentSectors_WeightedHigherThanDefensiveSectors()
    {
        // Directly encodes the ask: tech/AI sentiment should count for more than defensive sectors.
        Assert.True(SectorSentimentWeights.ForSector("Technology") > SectorSentimentWeights.ForSector("Utilities"));
        Assert.True(SectorSentimentWeights.ForSector("Technology") > SectorSentimentWeights.ForSector("Consumer Defensive"));
        Assert.True(SectorSentimentWeights.ForSector("Communication Services") > SectorSentimentWeights.ForSector("Utilities"));
    }
}
