using QuantHub.Core.Analysis;
using QuantHub.Desktop.Services;

namespace QuantHub.Desktop.Tests;

public class ScoreWeightsServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "QuantHubTests", Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private ScoreWeightsService NewService() => new(_dir);

    [Fact]
    public void NewInstance_WithNoPersistedFile_StartsAtDefaultWeights()
    {
        var service = NewService();

        Assert.Equal(QuantScoreCalculator.Weights.Default, service.Current);
    }

    [Fact]
    public void Apply_PersistsWeightsAcrossInstances()
    {
        var applied = new QuantScoreCalculator.Weights(Trend: 2.5, Momentum: 0.5);
        NewService().Apply(applied);

        var reloaded = NewService();

        Assert.Equal(applied, reloaded.Current);
    }

    [Fact]
    public void Reset_RestoresDefaultWeights()
    {
        var service = NewService();
        service.Apply(new QuantScoreCalculator.Weights(Trend: 3.0));

        service.Reset();

        Assert.Equal(QuantScoreCalculator.Weights.Default, service.Current);
    }

    [Fact]
    public void Apply_RaisesPropertyChangedForCurrent()
    {
        var service = NewService();
        var raisedFor = "";
        service.PropertyChanged += (_, e) => raisedFor = e.PropertyName;

        service.Apply(new QuantScoreCalculator.Weights(Trend: 1.5));

        Assert.Equal(nameof(ScoreWeightsService.Current), raisedFor);
    }
}
