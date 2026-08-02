using QuantHub.Core.Analysis;
using QuantHub.Core.Models;
using QuantHub.Core.Sentiment;
using QuantHub.Core.Yahoo;

namespace QuantHub.Core.Services;

/// <summary>
/// Orchestrates the per-ticker commands from stock_data.py (overview/history/fundamentals/news/
/// peers/analyst/insider) by composing YahooFinanceClient with the Indicators/QuantScoreCalculator/
/// analyzer modules. Market pulse and universe data are standalone (MarketPulseService,
/// Universe.UniverseData) since they don't depend on a specific ticker.
/// </summary>
public sealed class StockAnalysisService(YahooFinanceClient yahoo, SentimentService sentiment)
{
    private static readonly Dictionary<string, string> HistoryPeriodMap = new()
    {
        ["ytd"] = "ytd", ["6mo"] = "6mo", ["1y"] = "1y", ["2y"] = "2y", ["5y"] = "5y"
    };

    private static readonly Dictionary<string, string> PeersPeriodMap = new()
    {
        ["1y"] = "1y", ["5y"] = "5y"
    };

    /// <summary>Search-as-you-type ticker/company-name lookup, backing every autocomplete box in the
    /// app. Short-circuits blank/whitespace queries before ever touching the network - callers (all
    /// debounced) still fire this on very short input, and a 0-1 character query is never worth a
    /// round trip.</summary>
    public async Task<IReadOnlyList<TickerSearchResult>> SearchTickersAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var result = await yahoo.SearchAsync(query, ct);
        return result is { } r ? TickerSearchParser.Parse(r) : [];
    }

    public async Task<StockOverview?> GetOverviewAsync(string ticker, QuantScoreCalculator.Weights? weights = null, CancellationToken ct = default)
    {
        var upper = ticker.ToUpperInvariant();
        var barsTask = yahoo.GetChartAsync(upper, "1y", ct);
        var infoTask = yahoo.GetQuoteSummaryAsync(upper, ["price", "summaryDetail", "assetProfile", "defaultKeyStatistics"], ct);
        var sentimentTask = sentiment.FetchSentimentAsync(upper, ct);
        await Task.WhenAll(barsTask, infoTask, sentimentTask);

        var bars = barsTask.Result;
        if (bars is null || bars.Count == 0) return null;

        var closes = bars.Select(b => b.Close).ToArray();
        var volumes = bars.Select(b => b.Volume).ToArray();

        var latestClose = closes[^1];
        var prevClose = closes.Length > 1 ? closes[^2] : latestClose;
        var change = latestClose - prevClose;
        var changePct = prevClose != 0 ? change / prevClose * 100 : 0.0;

        var ma50 = Indicators.Sma(closes, 50)[^1];
        var ma200 = Indicators.Sma(closes, 200)[^1];
        var (macdArr, signalArr) = Indicators.Macd(closes);
        var macd = macdArr[^1] ?? 0.0;
        var macdSignal = signalArr[^1] ?? 0.0;
        var rsi = Indicators.Rsi(closes)[^1] ?? 50.0;
        var (bbUpperArr, bbLowerArr, _) = Indicators.BollingerBands(closes);
        var bbUpper = bbUpperArr[^1];
        var bbLower = bbLowerArr[^1];
        var roc21Pct = closes.Length > 21 ? (closes[^1] - closes[^22]) / closes[^22] * 100 : (double?)null;

        var latestVolume = volumes[^1];
        var avgVolumeFull = (long)volumes.Average(v => (double)v);

        var info = infoTask.Result;
        var name = upper;
        string? sector = null;
        double? beta = null;
        if (info is { } inf)
        {
            name = YahooJson.StrAny(inf, ["price", "assetProfile"], "shortName")
                   ?? YahooJson.Str(inf, "price", "longName")
                   ?? upper;
            sector = YahooJson.Str(inf, "assetProfile", "sector");
            beta = YahooJson.RawAny(inf, ["summaryDetail", "defaultKeyStatistics"], "beta");
        }

        var sentimentResult = sentimentTask.Result;
        var sentimentWeight = SectorSentimentWeights.ForSector(sector);

        var score = QuantScoreCalculator.Calculate(
            latestClose, ma50, ma200, rsi, macd, macdSignal,
            latestVolume, volumes, avgVolumeFull, bbUpper, bbLower, roc21Pct,
            sentimentResult.AverageScore, weights, sentimentWeight);

        var annVol = Indicators.AnnualizedVolatility(closes);
        var sharpe = Indicators.SharpeRatio(closes);
        var maxDd = Indicators.MaxDrawdownPercent(closes);

        return new StockOverview
        {
            Ticker = upper,
            Name = name,
            Price = Math.Round(latestClose, 4),
            Change = Math.Round(change, 4),
            ChangePercent = Math.Round(changePct, 4),
            QuantScore = Math.Round(score.QuantScore, 2),
            Signal = score.Signal,
            SentimentScore = Math.Round(sentimentResult.AverageScore, 4),
            Volume = latestVolume,
            AvgVolume = avgVolumeFull,
            Rsi = Math.Round(rsi, 2),
            Ma50 = ma50 is { } m5 ? Math.Round(m5, 4) : null,
            Ma200 = ma200 is { } m2 ? Math.Round(m2, 4) : null,
            Macd = Math.Round(macd, 4),
            MacdSignal = Math.Round(macdSignal, 4),
            Sector = sector,
            Beta = beta,
            AnnualizedVolatility = annVol is { } av ? Math.Round(av, 2) : null,
            SharpeRatio = sharpe is { } sh ? Math.Round(sh, 3) : null,
            MaxDrawdown = maxDd is { } md ? Math.Round(md, 2) : null,
            TrendScore = Math.Round(score.TrendScore, 2),
            MomentumScore = Math.Round(score.MomentumScore, 2),
            MacdScore = Math.Round(score.MacdScore, 2),
            SentimentContrib = score.SentimentContrib,
            SentimentWeightMultiplier = sentimentWeight,
            MeanReversionScore = Math.Round(score.MeanReversionScore, 2),
            PriceMomentumScore = Math.Round(score.PriceMomentumScore, 2),
            BollingerPctB = bbUpper is { } u && bbLower is { } l && u > l ? Math.Round((latestClose - l) / (u - l), 4) : null,
            PriceRoc21Pct = roc21Pct is { } r ? Math.Round(r, 2) : null,
            VolScore = Math.Round(score.VolScore, 2),
            VolRatio = Math.Round(score.VolRatio, 3),
            AboveMa50 = score.AboveMa50,
            AboveMa200 = score.AboveMa200,
            GoldenCross = score.GoldenCross
        };
    }

    public async Task<StockHistory?> GetHistoryAsync(string ticker, string period, CancellationToken ct = default)
    {
        var upper = ticker.ToUpperInvariant();
        var yfPeriod = HistoryPeriodMap.GetValueOrDefault(period, "1y");
        var bars = await yahoo.GetChartAsync(upper, yfPeriod, ct);
        if (bars is null || bars.Count == 0) return null;

        var closes = bars.Select(b => b.Close).ToArray();
        var ma50 = Indicators.Sma(closes, 50);
        var ma200 = Indicators.Sma(closes, 200);
        var (macd, signal) = Indicators.Macd(closes);
        var rsi = Indicators.Rsi(closes);
        var (bbUpper, bbLower, bbMa20) = Indicators.BollingerBands(closes);

        var priceBars = new List<PriceBar>(bars.Count);
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i];
            priceBars.Add(new PriceBar
            {
                Date = b.Date.ToString("yyyy-MM-dd"),
                Open = Math.Round(b.Open, 4),
                High = Math.Round(b.High, 4),
                Low = Math.Round(b.Low, 4),
                Close = Math.Round(b.Close, 4),
                Volume = b.Volume,
                Ma50 = Round4(ma50[i]),
                Ma200 = Round4(ma200[i]),
                Macd = Round4(macd[i]),
                MacdSignal = Round4(signal[i]),
                Rsi = Round4(rsi[i]),
                BbUpper = Round4(bbUpper[i]),
                BbLower = Round4(bbLower[i]),
                BbMa20 = Round4(bbMa20[i])
            });
        }

        return new StockHistory { Ticker = upper, Bars = priceBars };
    }

    public async Task<FundamentalsData?> GetFundamentalsAsync(string ticker, CancellationToken ct = default)
    {
        var upper = ticker.ToUpperInvariant();
        var result = await yahoo.GetQuoteSummaryAsync(upper, FundamentalsAnalyzer.Modules, ct);
        return result is { } r ? FundamentalsAnalyzer.Build(upper, r) : null;
    }

    public async Task<EarningsData?> GetEarningsAsync(string ticker, CancellationToken ct = default)
    {
        var upper = ticker.ToUpperInvariant();
        var result = await yahoo.GetQuoteSummaryAsync(upper, EarningsAnalyzer.Modules, ct);
        return result is { } r ? EarningsAnalyzer.Build(upper, r) : null;
    }

    public async Task<NewsData> GetNewsAsync(string ticker, CancellationToken ct = default)
    {
        var upper = ticker.ToUpperInvariant();
        var result = await sentiment.FetchSentimentAsync(upper, ct);
        var headlines = result.Headlines
            .Select(h => new NewsItem { Title = h.Title, Url = h.Url, PublishedAt = h.PublishedAt, Sentiment = h.Sentiment })
            .ToList();

        return new NewsData
        {
            Ticker = upper,
            SentimentScore = Math.Round(result.AverageScore, 4),
            SentimentLabel = SentimentService.SentimentLabel(result.AverageScore),
            Headlines = headlines
        };
    }

    public async Task<AnalystData?> GetAnalystAsync(string ticker, CancellationToken ct = default)
    {
        var upper = ticker.ToUpperInvariant();
        var result = await yahoo.GetQuoteSummaryAsync(upper, AnalystAnalyzer.Modules, ct);
        return result is { } r ? AnalystAnalyzer.Build(upper, r) : null;
    }

    public async Task<InsiderData?> GetInsiderAsync(string ticker, CancellationToken ct = default)
    {
        var upper = ticker.ToUpperInvariant();
        var result = await yahoo.GetQuoteSummaryAsync(upper, InsiderAnalyzer.Modules, ct);
        if (result is not { } r) return null;
        var name = YahooJson.StrAny(r, ["price", "assetProfile"], "shortName");
        return InsiderAnalyzer.Build(upper, r, name);
    }

    /// <summary>Resolves a ticker's sector + same-sector peer tickers via PeersAnalyzer, falling
    /// back to Yahoo's own assetProfile sector (matched against UniverseData) for tickers outside
    /// the hardcoded 11-sector universe. Shared by GetPeersAsync and GetSimilarStocksAsync so both
    /// "peers" and "similar stocks" mean the same thing throughout the app.</summary>
    private async Task<(string Sector, IReadOnlyList<string> Peers)> ResolveSectorAndPeersAsync(string upper, CancellationToken ct)
    {
        var (sector, peers) = PeersAnalyzer.GetPeersForTicker(upper);
        if (sector is not null) return (sector, peers);

        var subjectInfo = await yahoo.GetQuoteSummaryAsync(upper, ["assetProfile"], ct);
        var yahooSector = subjectInfo is { } si ? YahooJson.Str(si, "assetProfile", "sector") : null;
        sector = yahooSector ?? "Unknown";
        var sectorEntry = Universe.UniverseData.Sectors.FirstOrDefault(s => s.Sector == sector);
        peers = sectorEntry.Tickers?.Where(t => t != upper).ToArray() ?? [];
        return (sector, peers);
    }

    /// <summary>Lightweight "similar stocks" lookup for the Universe page - same-sector peers as
    /// GetPeersAsync, but only a quick current-price/change quote per ticker (no full price history,
    /// no correlation matrix) since the Universe cards just need a snapshot, not charts.</summary>
    public async Task<IReadOnlyList<SimilarStock>> GetSimilarStocksAsync(string ticker, int count = 6, CancellationToken ct = default)
    {
        var upper = ticker.ToUpperInvariant();
        var (sector, peers) = await ResolveSectorAndPeersAsync(upper, ct);
        var candidates = peers.Take(count).ToList();

        var results = new SimilarStock[candidates.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, candidates.Count), ct, async (i, token) =>
        {
            var t = candidates[i];
            var barsTask = yahoo.GetChartAsync(t, "5d", token);
            var infoTask = yahoo.GetQuoteSummaryAsync(t, ["price"], token);
            await Task.WhenAll(barsTask, infoTask);

            double? price = null;
            double? changePct = null;
            if (barsTask.Result is { Count: > 0 } bars)
            {
                var closes = bars.Select(b => b.Close).ToArray();
                price = closes[^1];
                if (closes.Length > 1 && closes[^2] != 0) changePct = (closes[^1] - closes[^2]) / closes[^2] * 100;
            }

            var name = infoTask.Result is { } info ? YahooJson.Str(info, "price", "shortName") : null;
            results[i] = new SimilarStock { Ticker = t, Name = name, Sector = sector, Price = price, ChangePercent = changePct };
        });

        return results;
    }

    public async Task<PeersData> GetPeersAsync(string ticker, string period, CancellationToken ct = default)
    {
        var upper = ticker.ToUpperInvariant();
        var yfPeriod = PeersPeriodMap.GetValueOrDefault(period, "1y");

        var (sector, peers) = await ResolveSectorAndPeersAsync(upper, ct);

        var compareList = new List<string> { upper };
        compareList.AddRange(peers.Take(6));

        var barsByTicker = new Dictionary<string, IReadOnlyList<Bar>>();
        var peerStocks = new List<PeerStock>();
        var gate = new object();

        await Parallel.ForEachAsync(compareList, ct, async (t, token) =>
        {
            var barsTask = yahoo.GetChartAsync(t, yfPeriod, token);
            var infoTask = yahoo.GetQuoteSummaryAsync(t, ["price", "summaryDetail", "financialData"], token);
            await Task.WhenAll(barsTask, infoTask);

            if (barsTask.Result is { } bars)
            {
                lock (gate) barsByTicker[t] = bars;
            }

            PeerStock stock;
            try
            {
                if (infoTask.Result is { } info)
                {
                    stock = new PeerStock
                    {
                        Ticker = t,
                        Name = YahooJson.Str(info, "price", "shortName"),
                        Price = YahooJson.Raw(info, "price", "regularMarketPrice") ?? YahooJson.Raw(info, "financialData", "currentPrice"),
                        Pe = YahooJson.Raw(info, "summaryDetail", "trailingPE"),
                        ForwardPe = YahooJson.Raw(info, "summaryDetail", "forwardPE"),
                        DividendYield = YahooJson.Raw(info, "summaryDetail", "dividendYield"),
                        Beta = YahooJson.Raw(info, "summaryDetail", "beta"),
                        MarketCap = YahooJson.Raw(info, "price", "marketCap") ?? YahooJson.Raw(info, "summaryDetail", "marketCap"),
                        ProfitMargins = YahooJson.Raw(info, "financialData", "profitMargins"),
                        DebtToEquity = YahooJson.Raw(info, "financialData", "debtToEquity"),
                        ReturnOnEquity = YahooJson.Raw(info, "financialData", "returnOnEquity")
                    };
                }
                else
                {
                    stock = new PeerStock { Ticker = t };
                }
            }
            catch
            {
                stock = new PeerStock { Ticker = t };
            }

            lock (gate) peerStocks.Add(stock);
        });

        var orderedPeers = compareList
            .Select(t => peerStocks.FirstOrDefault(p => p.Ticker == t) ?? new PeerStock { Ticker = t })
            .ToList();

        var correlation = PeersAnalyzer.BuildCorrelationMatrix(barsByTicker);
        var summary = PeersAnalyzer.GeneratePeersSummary(upper, orderedPeers, sector);

        return new PeersData
        {
            Ticker = upper,
            Sector = sector,
            Summary = summary,
            Peers = orderedPeers,
            CorrelationMatrix = correlation
        };
    }

    private static double? Round4(double? v) => v is { } x ? Math.Round(x, 4) : null;
}
