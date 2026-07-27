using QuantHub.Core.Models;
using QuantHub.Core.Yahoo;

namespace QuantHub.Core.MarketPulse;

/// <summary>Ports the market_pulse command (stock_data.py lines 684-772): hardcoded index/sector/macro
/// symbol lists, day/1W/1M % change via indices -1/-6/0 into a 1-month series, VIX mood thresholds,
/// and the sector-rotation note - including its literal "+" sign quirk on the best performer, which
/// renders as e.g. "+-0.3%" whenever the best 1-week performer is itself negative.</summary>
public sealed class MarketPulseService(YahooFinanceClient yahoo)
{
    private static readonly (string Symbol, string Label)[] Indices =
    [
        ("SPY", "S&P 500"), ("QQQ", "Nasdaq 100"), ("DIA", "Dow Jones"), ("IWM", "Russell 2000")
    ];

    private static readonly (string Symbol, string Label)[] SectorEtfs =
    [
        ("XLK", "Technology"), ("XLF", "Financials"), ("XLE", "Energy"), ("XLV", "Healthcare"),
        ("XLC", "Comm. Services"), ("XLI", "Industrials"), ("XLP", "Cons. Staples"),
        ("XLY", "Cons. Discret."), ("XLB", "Materials"), ("XLRE", "Real Estate"), ("XLU", "Utilities")
    ];

    private static readonly (string Symbol, string Label)[] MacroInstruments =
    [
        ("^VIX", "VIX (Fear)"), ("^TNX", "10Y Yield"), ("GLD", "Gold"),
        ("USO", "Oil"), ("UUP", "US Dollar"), ("BTC-USD", "Bitcoin")
    ];

    public async Task<MarketPulseData> GetMarketPulseAsync(CancellationToken ct = default)
    {
        var all = Indices.Concat(SectorEtfs).Concat(MacroInstruments).ToArray();
        var barsBySymbol = new Dictionary<string, IReadOnlyList<Bar>?>();
        var gate = new object();

        await Parallel.ForEachAsync(all, ct, async (item, token) =>
        {
            IReadOnlyList<Bar>? bars;
            try
            {
                bars = await yahoo.GetChartAsync(item.Symbol, "1mo", token);
            }
            catch
            {
                bars = null;
            }
            lock (gate) barsBySymbol[item.Symbol] = bars;
        });

        MarketPulseItem? MakeItem(string symbol, string label)
        {
            try
            {
                if (!barsBySymbol.TryGetValue(symbol, out var bars) || bars is null || bars.Count < 2) return null;
                var closes = bars.Select(b => b.Close).ToArray();
                var price = closes[^1];
                var prev = closes[^2];
                var weekAgo = closes.Length >= 6 ? closes[^6] : closes[0];
                var monthAgo = closes[0];

                double Pct(double a, double b) => b != 0 ? Math.Round((a - b) / b * 100, 2) : 0.0;

                return new MarketPulseItem
                {
                    Symbol = symbol,
                    Label = label,
                    Price = Math.Round(price, 4),
                    Change = Math.Round(price - prev, 4),
                    ChangePct = Pct(price, prev),
                    Change1wPct = Pct(price, weekAgo),
                    Change1mPct = Pct(price, monthAgo)
                };
            }
            catch
            {
                return null;
            }
        }

        var indices = Indices.Select(i => MakeItem(i.Symbol, i.Label)).Where(x => x is not null).Select(x => x!).ToList();
        var sectors = SectorEtfs.Select(s => MakeItem(s.Symbol, s.Label)).Where(x => x is not null).Select(x => x!).ToList();
        var macro = MacroInstruments.Select(m => MakeItem(m.Symbol, m.Label)).Where(x => x is not null).Select(x => x!).ToList();

        var vixItem = macro.FirstOrDefault(m => m.Symbol == "^VIX");
        var vix = vixItem?.Price ?? 20.0;
        var mood = ComputeMood(vix);
        var rotationNote = ComputeRotationNote(sectors);
        var sectorsFinal = sectors.OrderByDescending(s => s.ChangePct).ToList();

        return new MarketPulseData
        {
            Indices = indices,
            Sectors = sectorsFinal,
            Macro = macro,
            Vix = Math.Round(vix, 2),
            MarketMood = mood,
            RotationNote = rotationNote
        };
    }

    /// <summary>VIX mood thresholds from stock_data.py lines 748-753 - extracted as a pure function so
    /// the boundary values are unit-testable without a live network call.</summary>
    internal static string ComputeMood(double vix) => vix switch
    {
        >= 35 => "Extreme Fear",
        >= 25 => "Fear",
        >= 18 => "Neutral",
        >= 12 => "Greed",
        _ => "Extreme Greed"
    };

    /// <summary>Sector rotation note from stock_data.py lines 756-763. Deliberately preserves the
    /// literal "+" sign hardcoded before the best performer's value - if the "best" (least-bad or
    /// most-positive) 1-week performer is itself negative, this renders as e.g. "+-0.3%".</summary>
    internal static string ComputeRotationNote(IReadOnlyList<MarketPulseItem> sectors)
    {
        if (sectors.Count == 0) return "";
        var sortedByWeek = sectors.OrderByDescending(s => s.Change1wPct).ToList();
        var best = sortedByWeek[0];
        var worst = sortedByWeek[^1];
        return $"Money is rotating into {best.Label} (+{best.Change1wPct.ToString("0.0")}% 1W) and out of " +
               $"{worst.Label} ({worst.Change1wPct.ToString("0.0")}% 1W).";
    }
}
