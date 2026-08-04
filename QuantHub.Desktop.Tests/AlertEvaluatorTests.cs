using QuantHub.Core.Alerts;

namespace QuantHub.Desktop.Tests;

public class AlertEvaluatorTests
{
    private static PriceAlert Alert(AlertDirection direction, double target) =>
        new(Guid.NewGuid(), "AAPL", direction, target, DateTime.UtcNow, null, null);

    [Theory]
    [InlineData(AlertDirection.Above, 150, 150, true)]  // exactly at target counts as triggered
    [InlineData(AlertDirection.Above, 150, 151, true)]
    [InlineData(AlertDirection.Above, 150, 149.99, false)]
    [InlineData(AlertDirection.Below, 150, 150, true)]
    [InlineData(AlertDirection.Below, 150, 149, true)]
    [InlineData(AlertDirection.Below, 150, 150.01, false)]
    public void IsTriggered_ComparesCurrentPriceAgainstTarget(AlertDirection direction, double target, double currentPrice, bool expected)
    {
        Assert.Equal(expected, AlertEvaluator.IsTriggered(Alert(direction, target), currentPrice));
    }

    [Fact]
    public void FormatTriggerMessage_Above_ReadsNaturally()
    {
        var alert = Alert(AlertDirection.Above, 150) with { TriggeredAtPrice = 151.23 };

        var message = AlertEvaluator.FormatTriggerMessage(alert);

        Assert.Equal("AAPL is now above $150.00 (currently $151.23).", message);
    }

    [Fact]
    public void FormatTriggerMessage_Below_ReadsNaturally()
    {
        var alert = Alert(AlertDirection.Below, 150) with { TriggeredAtPrice = 148.50 };

        var message = AlertEvaluator.FormatTriggerMessage(alert);

        Assert.Equal("AAPL is now below $150.00 (currently $148.50).", message);
    }

    [Fact]
    public void FormatTriggerMessage_NoTriggeredPriceRecorded_FallsBackToTargetPrice()
    {
        var alert = Alert(AlertDirection.Above, 150);

        var message = AlertEvaluator.FormatTriggerMessage(alert);

        Assert.Equal("AAPL is now above $150.00 (currently $150.00).", message);
    }
}
