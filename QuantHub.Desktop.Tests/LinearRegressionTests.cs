using QuantHub.Core.Backtesting;

namespace QuantHub.Desktop.Tests;

public class LinearRegressionTests
{
    [Fact]
    public void FitCoefficients_SingleFeature_RecoversExactSlope()
    {
        double[][] x = [[1], [2], [3], [4], [5]];
        double[] y = [2, 4, 6, 8, 10]; // y = 2x exactly

        var coefficients = LinearRegression.FitCoefficients(x, y, ridge: 1e-9);

        Assert.Single(coefficients);
        Assert.Equal(2.0, coefficients[0], 3);
    }

    [Fact]
    public void FitCoefficients_SingleFeatureWithIntercept_RecoversSlopeIgnoringOffset()
    {
        double[][] x = [[1], [2], [3], [4], [5]];
        double[] y = [5, 7, 9, 11, 13]; // y = 3 + 2x

        var coefficients = LinearRegression.FitCoefficients(x, y, ridge: 1e-9);

        Assert.Equal(2.0, coefficients[0], 3);
    }

    [Fact]
    public void FitCoefficients_MultipleFeatures_RecoversEachCoefficientIndependently()
    {
        // y = 1*x1 + 2*x2 - 3*x3, exactly, across 6 well-conditioned (overdetermined) rows.
        double[][] x =
        [
            [1, 0, 0],
            [0, 1, 0],
            [0, 0, 1],
            [1, 1, 1],
            [1, -1, 2],
            [2, 1, -1]
        ];
        double[] y = [1, 2, -3, 0, -7, 7];

        var coefficients = LinearRegression.FitCoefficients(x, y, ridge: 1e-9);

        Assert.Equal(3, coefficients.Length);
        Assert.Equal(1.0, coefficients[0], 2);
        Assert.Equal(2.0, coefficients[1], 2);
        Assert.Equal(-3.0, coefficients[2], 2);
    }

    [Fact]
    public void FitCoefficients_IrrelevantFeature_GetsNearZeroCoefficient()
    {
        // y depends only on x1; x2 is unrelated noise-ish values with no consistent relationship to y.
        double[][] x =
        [
            [1, 5], [2, 1], [3, 9], [4, 2], [5, 7], [6, 3], [7, 8], [8, 4]
        ];
        double[] y = [2, 4, 6, 8, 10, 12, 14, 16]; // y = 2*x1 exactly, x2 ignored

        var coefficients = LinearRegression.FitCoefficients(x, y, ridge: 1e-9);

        Assert.Equal(2.0, coefficients[0], 2);
        Assert.True(Math.Abs(coefficients[1]) < 0.05, $"expected near-zero, got {coefficients[1]}");
    }

    [Fact]
    public void FitCoefficients_CollinearFeatures_DoesNotThrowOrProduceNaN()
    {
        // x2 is an exact duplicate of x1 - X^T X is singular without the ridge term.
        double[][] x = [[1, 1], [2, 2], [3, 3], [4, 4], [5, 5]];
        double[] y = [2, 4, 6, 8, 10];

        var coefficients = LinearRegression.FitCoefficients(x, y, ridge: 1e-6);

        Assert.Equal(2, coefficients.Length);
        Assert.All(coefficients, c => Assert.False(double.IsNaN(c)));
        Assert.All(coefficients, c => Assert.False(double.IsInfinity(c)));
    }

    [Fact]
    public void FitCoefficients_ConstantSecondFeature_DoesNotDistortFirstCoefficient()
    {
        // x2 is constant (5 every time) - genuinely no explanatory power beyond the intercept.
        double[][] x = [[1, 5], [2, 5], [3, 5], [4, 5], [5, 5]];
        double[] y = [2, 4, 6, 8, 10]; // y = 2*x1, x2 irrelevant

        var coefficients = LinearRegression.FitCoefficients(x, y, ridge: 1e-6);

        Assert.Equal(2.0, coefficients[0], 1);
    }
}
