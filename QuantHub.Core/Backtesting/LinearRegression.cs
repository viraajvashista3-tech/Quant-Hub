namespace QuantHub.Core.Backtesting;

/// <summary>
/// Ordinary least squares via the normal equations, solved by Gauss-Jordan elimination with partial
/// pivoting - used to fit all of BacktestEngine's technical components jointly against forward
/// returns, instead of correlating each one independently. Independent (Pearson) correlation credits
/// two components that move together (e.g. Trend and a momentum-ish factor) for the same shared
/// signal twice; a joint fit answers "how much does this component add once the others are already
/// accounted for," which is what "how should these be weighted relative to each other" actually
/// requires.
///
/// A small ridge (L2) term is added to the diagonal before solving so near-collinear features (which
/// six correlated technical signals often are) don't produce a singular or numerically unstable
/// system - without it, Gauss-Jordan on a near-singular matrix can return wildly inflated or NaN
/// coefficients for no real underlying reason.
/// </summary>
public static class LinearRegression
{
    /// <summary>Fits y ~ b0 + b1*x1 + ... + bp*xp and returns [b1..bp] (the intercept b0 is fit but
    /// dropped from the result - BacktestEngine only needs the slopes to redistribute its point
    /// budget). x[i] is sample i's feature vector; all rows must have the same length.</summary>
    public static double[] FitCoefficients(IReadOnlyList<double[]> x, IReadOnlyList<double> y, double ridge = 1e-6)
    {
        var n = x.Count;
        var p = x[0].Length;
        var k = p + 1; // +1 for the intercept column

        double Design(int row, int col) => col == 0 ? 1.0 : x[row][col - 1];

        // Normal equations: (X^T X + ridge*I) beta = X^T y
        var xtx = new double[k, k];
        var xty = new double[k];
        for (var i = 0; i < k; i++)
        {
            for (var j = i; j < k; j++)
            {
                double sum = 0;
                for (var r = 0; r < n; r++) sum += Design(r, i) * Design(r, j);
                xtx[i, j] = sum;
                xtx[j, i] = sum;
            }
            xtx[i, i] += ridge;

            double sumY = 0;
            for (var r = 0; r < n; r++) sumY += Design(r, i) * y[r];
            xty[i] = sumY;
        }

        var beta = Solve(xtx, xty);
        return beta.Skip(1).ToArray();
    }

    /// <summary>Solves the k x k linear system Ax=b via Gauss-Jordan elimination with partial
    /// pivoting, returning x. If a pivot is (numerically) zero even after the ridge term, that
    /// coefficient is left at 0 rather than dividing by ~zero - a degenerate direction contributes
    /// nothing rather than blowing up.</summary>
    private static double[] Solve(double[,] a, double[] b)
    {
        var n = b.Length;
        var m = new double[n, n + 1];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++) m[i, j] = a[i, j];
            m[i, n] = b[i];
        }

        for (var col = 0; col < n; col++)
        {
            var pivotRow = col;
            for (var r = col + 1; r < n; r++)
            {
                if (Math.Abs(m[r, col]) > Math.Abs(m[pivotRow, col])) pivotRow = r;
            }
            if (pivotRow != col)
            {
                for (var j = 0; j <= n; j++) (m[col, j], m[pivotRow, j]) = (m[pivotRow, j], m[col, j]);
            }

            var diag = m[col, col];
            if (Math.Abs(diag) < 1e-10) continue; // degenerate direction - leave this row's contribution at 0

            for (var j = col; j <= n; j++) m[col, j] /= diag;

            for (var r = 0; r < n; r++)
            {
                if (r == col) continue;
                var factor = m[r, col];
                if (factor == 0) continue;
                for (var j = col; j <= n; j++) m[r, j] -= factor * m[col, j];
            }
        }

        var result = new double[n];
        for (var i = 0; i < n; i++) result[i] = m[i, n];
        return result;
    }
}
