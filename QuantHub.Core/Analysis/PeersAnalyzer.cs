using QuantHub.Core.Models;
using QuantHub.Core.Yahoo;

namespace QuantHub.Core.Analysis;

/// <summary>
/// Ports get_peers_for_ticker, the correlation-matrix construction, and generate_peers_summary
/// from stock_data.py lines 131-135 and 363-513.
/// </summary>
public static class PeersAnalyzer
{
    public static (string? Sector, IReadOnlyList<string> Peers) GetPeersForTicker(string ticker)
    {
        var upper = ticker.ToUpperInvariant();
        foreach (var (sector, tickers) in Universe.UniverseData.Sectors)
        {
            if (tickers.Contains(upper))
            {
                return (sector, tickers.Where(t => t != upper).ToArray());
            }
        }
        return (null, []);
    }

    /// <summary>Forward-fills then back-fills each ticker's series onto a shared date index before
    /// computing Pearson correlation of daily returns - matching the ffill().bfill() step in the
    /// original. Any failure yields an empty matrix, matching the original's silent try/except.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> BuildCorrelationMatrix(
        IReadOnlyDictionary<string, IReadOnlyList<Bar>> barsByTicker)
    {
        try
        {
            var tickers = barsByTicker.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key).ToList();
            if (tickers.Count < 2) return new Dictionary<string, IReadOnlyDictionary<string, double>>();

            var allDates = barsByTicker.Values.SelectMany(b => b.Select(x => x.Date)).Distinct().OrderBy(d => d).ToList();

            var series = new Dictionary<string, double?[]>();
            foreach (var t in tickers)
            {
                var byDate = barsByTicker[t].ToDictionary(b => b.Date, b => b.Close);
                var arr = new double?[allDates.Count];
                for (var i = 0; i < allDates.Count; i++)
                {
                    arr[i] = byDate.TryGetValue(allDates[i], out var c) ? c : null;
                }

                double? last = null;
                for (var i = 0; i < arr.Length; i++)
                {
                    if (arr[i] is { } v) last = v; else arr[i] = last;
                }

                double? next = null;
                for (var i = arr.Length - 1; i >= 0; i--)
                {
                    if (arr[i] is { } v) next = v; else arr[i] = next;
                }

                series[t] = arr;
            }

            var validTickers = series.Where(kv => kv.Value.Any(v => v is not null)).Select(kv => kv.Key).ToList();

            var returns = new Dictionary<string, double[]>();
            foreach (var t in validTickers)
            {
                var arr = series[t];
                var rets = new List<double>();
                for (var i = 1; i < arr.Length; i++)
                {
                    if (arr[i] is { } cur && arr[i - 1] is { } prev && prev != 0)
                        rets.Add((cur - prev) / prev);
                }
                returns[t] = rets.ToArray();
            }

            var result = new Dictionary<string, IReadOnlyDictionary<string, double>>();
            foreach (var a in validTickers)
            {
                var row = new Dictionary<string, double>();
                foreach (var b in validTickers)
                {
                    if (PearsonCorrelation(returns[a], returns[b]) is { } corr)
                    {
                        row[b] = Math.Round(corr, 4);
                    }
                }
                result[a] = row;
            }
            return result;
        }
        catch
        {
            return new Dictionary<string, IReadOnlyDictionary<string, double>>();
        }
    }

    private static double? PearsonCorrelation(double[] x, double[] y)
    {
        var n = Math.Min(x.Length, y.Length);
        if (n < 2) return null;
        var mx = x.Take(n).Average();
        var my = y.Take(n).Average();
        double sumXY = 0, sumX2 = 0, sumY2 = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = x[i] - mx;
            var dy = y[i] - my;
            sumXY += dx * dy;
            sumX2 += dx * dx;
            sumY2 += dy * dy;
        }
        if (sumX2 == 0 || sumY2 == 0) return null;
        return sumXY / Math.Sqrt(sumX2 * sumY2);
    }

    /// <summary>Ports generate_peers_summary. Every threshold below is verbatim from the original;
    /// see PythonCapitalize for the deliberately-preserved str.capitalize() quirk.</summary>
    public static string? GeneratePeersSummary(string ticker, IReadOnlyList<PeerStock> peerData, string sector)
    {
        try
        {
            var upper = ticker.ToUpperInvariant();
            var subject = peerData.FirstOrDefault(p => p.Ticker == upper);
            if (subject is null) return null;
            var peersOnly = peerData.Where(p => p.Ticker != upper).ToList();

            double? Median(Func<PeerStock, double?> selector)
            {
                var vals = peersOnly.Select(selector).Where(v => v is not null).Select(v => v!.Value).OrderBy(v => v).ToArray();
                if (vals.Length == 0) return null;
                var mid = vals.Length / 2;
                return vals.Length % 2 == 0 ? (vals[mid - 1] + vals[mid]) / 2.0 : vals[mid];
            }

            var name = subject.Name ?? upper;
            var pe = subject.Pe;
            var medPe = Median(p => p.Pe);
            var margins = subject.ProfitMargins;
            var medMargins = Median(p => p.ProfitMargins);
            var beta = subject.Beta;
            var medBeta = Median(p => p.Beta);
            var roe = subject.ReturnOnEquity;
            var medRoe = Median(p => p.ReturnOnEquity);
            var de = subject.DebtToEquity;
            var medDe = Median(p => p.DebtToEquity);

            var parts = new List<string>();

            if (pe is { } peV && medPe is { } medPeV)
            {
                var diffPct = medPeV != 0 ? (peV - medPeV) / medPeV * 100 : 0;
                if (Math.Abs(diffPct) > 10)
                {
                    var direction = diffPct > 0 ? "commands a premium" : "trades at a discount";
                    parts.Add($"{name} {direction} valuation vs its {sector} peers (P/E {peV:0.0}x vs sector median {medPeV:0.0}x)");
                }
                else
                {
                    parts.Add($"{name} trades in line with {sector} peers on valuation (P/E {peV:0.0}x)");
                }
            }

            if (margins is { } marginsV && medMargins is { } medMarginsV)
            {
                if (marginsV > medMarginsV * 1.1)
                {
                    parts.Add($"it leads the group on profitability with {marginsV * 100:0.0}% net margins (sector median {medMarginsV * 100:0.0}%)");
                }
                else if (marginsV < medMarginsV * 0.9)
                {
                    parts.Add($"its {marginsV * 100:0.0}% net margins trail the sector median of {medMarginsV * 100:0.0}%");
                }
                else
                {
                    parts.Add($"profit margins are in line with the sector at {marginsV * 100:0.0}%");
                }
            }

            if (beta is { } betaV && medBeta is { } medBetaV)
            {
                if (betaV > medBetaV * 1.15)
                {
                    parts.Add($"the stock carries above-average market risk (beta {betaV:0.00} vs sector {medBetaV:0.00})");
                }
                else if (betaV < medBetaV * 0.85)
                {
                    parts.Add($"it is less volatile than its peers (beta {betaV:0.00} vs sector {medBetaV:0.00})");
                }
            }

            if (roe is { } roeV && medRoe is { } medRoeV)
            {
                if (roeV > medRoeV * 1.1)
                {
                    parts.Add($"and generates stronger returns on equity ({roeV * 100:0.0}% vs sector {medRoeV * 100:0.0}%)");
                }
                else if (roeV < medRoeV * 0.9)
                {
                    parts.Add($"though return on equity lags peers ({roeV * 100:0.0}% vs {medRoeV * 100:0.0}%)");
                }
            }

            if (de is { } deV && medDe is { } medDeV)
            {
                if (deV > medDeV * 1.3)
                {
                    parts.Add($"Debt levels are elevated relative to peers (D/E {deV:0.0} vs {medDeV:0.0})");
                }
                else if (deV < medDeV * 0.7)
                {
                    parts.Add($"The balance sheet is relatively clean with lower debt than peers (D/E {deV:0.0} vs {medDeV:0.0})");
                }
            }

            if (parts.Count == 0)
            {
                return $"{name} shows broadly similar characteristics to its {sector} sector peers.";
            }

            return string.Join(". ", parts.Select(PythonCapitalize)) + ".";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Replicates Python's str.capitalize(): uppercase the first character, lowercase every
    /// other character. This mangles embedded proper nouns/abbreviations (company names, "P/E") in
    /// the original - kept exactly as-is rather than "fixed", per the byte-for-byte parity goal.</summary>
    internal static string PythonCapitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
    }
}
