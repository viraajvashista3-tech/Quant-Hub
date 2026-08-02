using QuantHub.Core.Analysis;
using QuantHub.Core.Models;
using QuantHub.Core.Services;

namespace QuantHub.Core.Backtesting;

/// <summary>
/// Walks each ticker's full available price history, computing what the seven backtestable Quant
/// Score components (Trend/Momentum/Macd/Vol/MeanReversion/PriceMomentum/RelativeStrength -
/// everything except Sentiment, which has no historical news archive to validate against) would have
/// scored at every past bar, alongside what the stock actually did over the following N trading days.
///
/// Weights are fit by ordinary least squares (see LinearRegression), not by correlating each
/// component with forward returns independently: several of these signals move together (Trend and
/// PriceMomentum both track recent price direction, for instance), so crediting each one with its own
/// marginal correlation double-counts shared signal. A joint fit answers "how much does this
/// component add once the others are already accounted for." Per-component Pearson correlation is
/// still computed and shown (ComponentStat.Correlation) as the simpler, marginal-relationship
/// complement to the regression-derived weight - seeing both together makes it visible when a
/// component's marginal correlation and its regression-implied weight disagree (a sign of
/// multicollinearity with another factor), not just one blended number.
///
/// Recalibrated weights are fit jointly across all four look-ahead horizons the Backtest page offers
/// (<see cref="CanonicalHorizons"/>: 5/10/20/60 trading days), not just whichever one is currently
/// selected in the UI. Fitting a single horizon independently off only a handful of walk-forward folds
/// is noisy, and several components are collinear - both push individual per-horizon fits toward
/// unstable, sometimes sign-flipping coefficients (observed empirically: Trend's fitted coefficient
/// swings and even changes sign as the selected horizon changes). Fitting each of the four horizons
/// separately and averaging the resulting weight vectors (<see cref="AverageWeights"/>) trades away
/// whatever genuinely horizon-specific signal exists (mean-reversion legitimately strengthens at
/// longer horizons, for instance) in exchange for one weight set that behaves reasonably regardless of
/// which horizon a caller cares about - the right tradeoff here since RecalibratedWeights feeds
/// QuantScoreCalculator, which is not itself horizon-specific. The selected horizon still controls the
/// walk-forward out-of-sample outcome tables (Buy/Hold/Avoid hit rates) and the marginal-correlation
/// column shown in the UI, since "what would have happened if I'd looked N days ahead" is a
/// legitimately horizon-specific question - only the fitted weights themselves are pooled.
///
/// Recalibration is evaluated by expanding-window walk-forward validation, not a single in-sample
/// fit: each ticker's samples (per horizon) are split into <see cref="Folds"/> chronological chunks,
/// and for each step the weights are recalibrated using only the chunks *before* the held-out one
/// (fit per horizon, then averaged, exactly as for the final weights), then evaluated only on the
/// selected horizon's held-out (still-future-relative-to-training) chunk. The Current-vs-Recalibrated
/// signal-outcome tables report the aggregate of those out-of-sample evaluations, so "recalibrated
/// looks better" (if it does) means it held up on data the fit never saw - not that it was fit and
/// graded on the same data, which would look good regardless of whether it actually works. The final
/// RecalibratedWeights offered for Apply are a separate fit using each horizon's *entire* dataset (the
/// best use of all available history once walk-forward has shown whether recalibrating is worthwhile
/// at all), then averaged the same way.
///
/// Causality: every input (Ma50/Ma200/Rsi/Macd/BollingerBands from
/// StockAnalysisService.GetHistoryAsync, volume ratio from Indicators.RollingVolumeRatio, 21-day rate
/// of change and the sector-peer comparison behind RelativeStrength, both computed from strictly
/// earlier bars in the same series) is a rolling calculation that only looks backward from each bar -
/// no future data leaks into a historical "prediction". Only the forward-return label looks ahead,
/// which is the whole point: it's the known outcome recalibration measures against.
///
/// The label itself is excess return over <see cref="BenchmarkTicker"/> (SPY), not a stock's raw
/// forward return: a horizon sweep (see backtest_feature memory, update #2) found that raw Buy hit
/// rate rises with horizon (54.6% -> 60.6% from 5d to 60d) while raw Avoid hit rate falls over the
/// same stretch (29.8% before the fix in this update) - both are exactly what you'd see if "looks
/// better at longer horizons" were mostly the market's own upward drift (the equity risk premium)
/// leaking into the label, not the model's signal actually improving. Subtracting SPY's return over
/// the identical date range (looked up by calendar date, same alignment technique as the sector-peer
/// comparison behind RelativeStrength, not by bar index - tickers and the benchmark don't always
/// share bar counts) removes that drift from every correlation, regression fit, and Buy/Avoid outcome
/// in this engine, so "predictive" means "beat/lagged the market", not "went up because everything
/// did". If SPY's own history can't be fetched, every sample lookup fails and the run correctly
/// surfaces as zero usable samples (the same degrade path already used when a ticker's own history
/// fails) rather than silently falling back to a drift-contaminated raw-return label.
/// </summary>
public sealed class BacktestEngine(StockAnalysisService stockAnalysis)
{
    /// <summary>Forward returns are measured relative to this benchmark (S&amp;P 500 ETF), not in
    /// absolute terms - see the class remarks above for why.</summary>
    public const string BenchmarkTicker = "SPY";

    private const double TotalBudget = QuantScoreCalculator.TrendMax + QuantScoreCalculator.MomentumMax
        + QuantScoreCalculator.MacdMax + QuantScoreCalculator.VolMax
        + QuantScoreCalculator.MeanReversionMax + QuantScoreCalculator.PriceMomentumMax
        + QuantScoreCalculator.RelativeStrengthMax + QuantScoreCalculator.InsiderPurchaseMax
        + QuantScoreCalculator.EarningsSurpriseMax;

    /// <summary>The four look-ahead windows the Backtest page's horizon pills offer
    /// (BacktestViewModel.HorizonOptions: 1W/2W/1M/3M) - weight recalibration always fits across all
    /// four and averages, regardless of which one is currently selected in the UI, so the applied
    /// weights aren't overfit to whichever single horizon happens to be selected.</summary>
    public static readonly int[] CanonicalHorizons = [5, 10, 20, 60];

    /// <summary>Caps how many tickers' network fetches phase 1 fires at once. Yahoo's chart/
    /// quoteSummary endpoints are unauthenticated and undocumented (see YahooFinanceClient's crumb/
    /// cookie handshake) - letting Parallel.ForEachAsync's default (Environment.ProcessorCount, but
    /// uncapped for I/O-bound work in practice) fire all 138 universe tickers' requests at once risks
    /// transient failures or throttling that would otherwise just show up as inflated "skipped"
    /// counts. 16 keeps a full-universe sweep fast (a handful of seconds' worth of batches) while
    /// never holding more than 16 connections open to the same host at once.</summary>
    private const int MaxNetworkConcurrency = 16;

    private readonly record struct Sample(
        double Trend, double Momentum, double Macd, double Vol, double MeanReversion, double PriceMomentum,
        double RelativeStrength, double InsiderPurchase, double EarningsSurprise, double ExcessReturnPct);

    public async Task<BacktestReport> RunAsync(
        IReadOnlyList<string> tickers,
        int horizonTradingDays = 10,
        int folds = 4,
        CancellationToken ct = default)
    {
        folds = Math.Max(folds, 2);
        // Always sample every canonical horizon (for weight fitting) plus whichever horizon the
        // caller actually asked for (for the out-of-sample outcome tables/correlation column) -
        // usually the same set, since the UI only ever offers the four canonical values.
        var horizons = CanonicalHorizons.Append(horizonTradingDays).Distinct().OrderBy(h => h).ToArray();
        var minHorizon = horizons[0];

        var gate = new object();
        var skipped = new List<string>();
        var barsByTicker = new Dictionary<string, IReadOnlyList<PriceBar>>();
        var insiderPurchaseDatesByTicker = new Dictionary<string, string[]>();
        var earningsEventsByTicker = new Dictionary<string, (string Date, double SurprisePercent)[]>();

        // Fetched alongside the universe (not part of it) - every forward-return label is measured
        // against this, so sampling below can't start until it's in hand either.
        var benchmarkTask = FetchBarsAsync(BenchmarkTicker, ct);

        // Phase 1: fetch every ticker's history first - RelativeStrength needs each ticker's sector
        // peers' bars too, so sampling can't start until every ticker's data is in hand. Insider
        // Purchase dates are fetched alongside (same phase, same per-ticker parallelism) - unlike bars,
        // Yahoo only returns each ticker's 50 most recent filings, so this typically covers roughly the
        // last ~2 years, not the full 5y price window; bars older than that simply get no insider
        // signal (InsiderPurchaseSignal(null) => 0), the same "missing data -> neutral" pattern already
        // used for Ma200/BollingerBands on early bars.
        var networkOptions = new ParallelOptions { MaxDegreeOfParallelism = MaxNetworkConcurrency, CancellationToken = ct };
        await Parallel.ForEachAsync(tickers, networkOptions, async (ticker, token) =>
        {
            var barsTask = FetchBarsAsync(ticker, token);
            var insiderTask = FetchInsiderPurchaseDatesAsync(ticker, token);
            var earningsTask = FetchEarningsSurpriseEventsAsync(ticker, token);
            await Task.WhenAll(barsTask, insiderTask, earningsTask);
            var bars = barsTask.Result;
            if (bars is null || bars.Count < 220)
            {
                lock (gate) skipped.Add(ticker);
                return;
            }
            lock (gate)
            {
                barsByTicker[ticker] = bars;
                insiderPurchaseDatesByTicker[ticker] = insiderTask.Result;
                earningsEventsByTicker[ticker] = earningsTask.Result;
            }
        });

        var benchmarkBars = await benchmarkTask;
        // Date -> close, not bar-index - the benchmark's bar count won't generally match any given
        // ticker's, so every lookup below goes through this map rather than assuming aligned indices.
        var benchmarkCloseByDate = (benchmarkBars ?? [])
            .ToDictionary(b => b.Date, b => b.Close);

        // Date -> close per ticker, so a peer's return can be looked up by calendar date rather than
        // assuming bar-index alignment (tickers can have slightly different bar counts).
        var closeByDateByTicker = barsByTicker.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToDictionary(b => b.Date, b => b.Close));

        var tickerList = barsByTicker.Keys.ToList();
        // samplesByHorizon[h][ticker] - built in one pass per ticker (shared per-bar signal
        // computation reused across every horizon; only the forward-return label differs per horizon).
        var samplesByHorizon = horizons.ToDictionary(h => h, _ => new Dictionary<string, List<Sample>>());

        // Phase 2: per-ticker sampling - CPU-bound, but kept as Parallel.ForEachAsync for consistency
        // with phase 1 and to spread the work across cores.
        await Parallel.ForEachAsync(tickerList, ct, async (ticker, _) =>
        {
            await Task.Yield();
            var bars = barsByTicker[ticker];
            var volumes = bars.Select(b => b.Volume).ToArray();
            var volRatios = Indicators.RollingVolumeRatio(volumes);
            var (_, peers) = PeersAnalyzer.GetPeersForTicker(ticker);
            var peerDateMaps = peers
                .Where(closeByDateByTicker.ContainsKey)
                .Select(p => closeByDateByTicker[p])
                .ToList();
            var purchaseDates = insiderPurchaseDatesByTicker.GetValueOrDefault(ticker, []);
            var purchasePtr = 0;
            string? lastPurchaseDate = null;

            var earningsEvents = earningsEventsByTicker.GetValueOrDefault(ticker, []);
            var earningsPtr = 0;
            string? lastEarningsDate = null;
            double? lastEarningsSurprise = null;

            var tickerSamplesByHorizon = horizons.ToDictionary(h => h, _ => new List<Sample>());
            for (var bar = 0; bar < bars.Count - minHorizon; bar++)
            {
                var b = bars[bar];

                // Two-pointer walk: both purchaseDates and bars are chronologically sorted, so the
                // most recent Purchase filing on or before this bar's date only ever advances forward -
                // must run even on bars skipped below for missing Ma200, or dates could be missed.
                while (purchasePtr < purchaseDates.Length && string.CompareOrdinal(purchaseDates[purchasePtr], b.Date) <= 0)
                {
                    lastPurchaseDate = purchaseDates[purchasePtr];
                    purchasePtr++;
                }

                // Same two-pointer walk as insider Purchase dates above, but also carries forward the
                // triggering quarter's surprise% (not just a date) - the signal needs both to compute a
                // decayed, direction-aware contribution.
                while (earningsPtr < earningsEvents.Length && string.CompareOrdinal(earningsEvents[earningsPtr].Date, b.Date) <= 0)
                {
                    lastEarningsDate = earningsEvents[earningsPtr].Date;
                    lastEarningsSurprise = earningsEvents[earningsPtr].SurprisePercent;
                    earningsPtr++;
                }

                if (b.Ma200 is null) continue; // not enough history yet at this point in time
                if (volRatios[bar] is not { } volRatio) continue;

                var trend = QuantScoreCalculator.TrendSignal(b.Close, b.Ma50, b.Ma200);
                var momentum = QuantScoreCalculator.MomentumSignal(b.Rsi);
                var macd = QuantScoreCalculator.MacdSignal(b.Macd, b.MacdSignal, b.Close);
                var vol = QuantScoreCalculator.VolumeSignal(volRatio);
                var meanReversion = QuantScoreCalculator.MeanReversionSignal(b.Close, b.BbUpper, b.BbLower);

                double? daysSincePurchase = null;
                if (lastPurchaseDate is not null && DateTime.TryParse(b.Date, out var bd) && DateTime.TryParse(lastPurchaseDate, out var pd))
                    daysSincePurchase = (bd - pd).TotalDays;
                var insiderPurchase = QuantScoreCalculator.InsiderPurchaseSignal(daysSincePurchase);

                double? daysSinceEarnings = null;
                if (lastEarningsDate is not null && DateTime.TryParse(b.Date, out var bd2) && DateTime.TryParse(lastEarningsDate, out var ed))
                    daysSinceEarnings = (bd2 - ed).TotalDays;
                var earningsSurprise = QuantScoreCalculator.EarningsSurpriseSignal(lastEarningsSurprise, daysSinceEarnings);

                double? roc21Pct = null;
                if (bar >= 21 && bars[bar - 21].Close != 0)
                    roc21Pct = (b.Close - bars[bar - 21].Close) / bars[bar - 21].Close * 100;
                var priceMomentum = QuantScoreCalculator.PriceMomentumSignal(roc21Pct);

                double? excessRoc21Pct = null;
                if (roc21Pct is { } ownRoc && bar >= 21 && peerDateMaps.Count > 0)
                {
                    var curDate = b.Date;
                    var pastDate = bars[bar - 21].Date;
                    var peerRocs = new List<double>();
                    foreach (var peerMap in peerDateMaps)
                    {
                        if (peerMap.TryGetValue(curDate, out var peerNow)
                            && peerMap.TryGetValue(pastDate, out var peerPast)
                            && peerPast != 0)
                        {
                            peerRocs.Add((peerNow - peerPast) / peerPast * 100);
                        }
                    }
                    if (peerRocs.Count > 0) excessRoc21Pct = ownRoc - peerRocs.Average();
                }
                var relativeStrength = QuantScoreCalculator.RelativeStrengthSignal(excessRoc21Pct);
                if (b.Close == 0) continue; // degenerate bar - can't compute a forward return off it

                foreach (var h in horizons)
                {
                    if (bar + h >= bars.Count) continue; // this horizon doesn't reach far enough for this bar
                    var futureBar = bars[bar + h];
                    // Benchmark must have a matching close on both the "now" and "future" dates, or the
                    // excess-return label can't be computed for this bar/horizon - skip rather than fall
                    // back to a raw (drift-contaminated) return, per the class remarks above.
                    if (!benchmarkCloseByDate.TryGetValue(b.Date, out var benchNow)
                        || !benchmarkCloseByDate.TryGetValue(futureBar.Date, out var benchFuture)
                        || benchNow == 0)
                        continue;

                    var excessReturnPct = ExcessReturnPct(b.Close, futureBar.Close, benchNow, benchFuture);
                    tickerSamplesByHorizon[h].Add(new Sample(
                        trend, momentum, macd, vol, meanReversion, priceMomentum, relativeStrength, insiderPurchase,
                        earningsSurprise, excessReturnPct));
                }
            }

            lock (gate)
            {
                foreach (var h in horizons) samplesByHorizon[h][ticker] = tickerSamplesByHorizon[h];
            }
        });

        var tickerCount = tickerList.Count;

        var allSamplesByHorizon = horizons.ToDictionary(
            h => h, h => tickerList.SelectMany(t => samplesByHorizon[h][t]).ToList());

        // Each ticker's samples chunked into `folds` chronological pieces (oldest first) - per horizon,
        // since each horizon has its own sample list (longer horizons drop more trailing bars).
        var chunkedByHorizon = horizons.ToDictionary(
            h => h,
            h => tickerList.Select(t => ChunkChronologically(samplesByHorizon[h][t], folds)).ToList());

        var currentFoldStats = new List<IReadOnlyList<SignalStats>>();
        var recalibratedFoldStats = new List<IReadOnlyList<SignalStats>>();
        var outOfSampleCount = 0;
        var walkForwardSteps = 0;
        var selectedChunks = chunkedByHorizon[horizonTradingDays];

        for (var step = 1; step < folds; step++)
        {
            var testSamples = new List<Sample>();
            foreach (var chunks in selectedChunks) testSamples.AddRange(chunks[step]);
            if (testSamples.Count == 0) continue;

            // Fit each canonical horizon's weights from its own training chunks (same step index, so
            // all horizons train on the same chronological window), then average - this validates the
            // *actual* multi-horizon recalibration approach out-of-sample, not a single-horizon
            // shortcut that happens to get evaluated against a different horizon's test data.
            var foldWeightsPerHorizon = new List<QuantScoreCalculator.Weights>();
            foreach (var h in CanonicalHorizons)
            {
                var trainSamples = new List<Sample>();
                foreach (var chunks in chunkedByHorizon[h])
                    for (var c = 0; c < step; c++) trainSamples.AddRange(chunks[c]);
                if (trainSamples.Count == 0) continue;
                var (weights, _) = FitWeights(trainSamples);
                foldWeightsPerHorizon.Add(weights);
            }
            if (foldWeightsPerHorizon.Count == 0) continue;
            var foldWeights = AverageWeights(foldWeightsPerHorizon);

            currentFoldStats.Add(BucketBySignal(testSamples, QuantScoreCalculator.Weights.Default));
            recalibratedFoldStats.Add(BucketBySignal(testSamples, foldWeights));
            outOfSampleCount += testSamples.Count;
            walkForwardSteps++;
        }

        var currentSignalStats = MergeSignalStats(currentFoldStats);
        var recalibratedSignalStats = MergeSignalStats(recalibratedFoldStats);

        // Final weights (what gets offered via Apply) are fit per canonical horizon on that horizon's
        // entire dataset, then averaged - walk-forward above already answered "does recalibrating
        // generalize", this refit just uses all available history for the version that actually goes
        // live, same as any model's final production fit, pooled across horizons for stability.
        var finalWeights = AverageWeights(
            CanonicalHorizons.Select(h => FitWeights(allSamplesByHorizon[h]).Weights).ToList());

        // Marginal correlation shown alongside the pooled weight is specific to the selected horizon -
        // "how did this component relate to forward returns over exactly this many days" is a
        // legitimately horizon-specific number, unlike the weight itself.
        var (_, corr) = FitWeights(allSamplesByHorizon[horizonTradingDays]);
        var components = new List<ComponentStat>
        {
            new("Trend", corr.Trend, QuantScoreCalculator.TrendMax, 1.0, finalWeights.Trend),
            new("Momentum", corr.Momentum, QuantScoreCalculator.MomentumMax, 1.0, finalWeights.Momentum),
            new("Macd", corr.Macd, QuantScoreCalculator.MacdMax, 1.0, finalWeights.Macd),
            new("Vol", corr.Vol, QuantScoreCalculator.VolMax, 1.0, finalWeights.Vol),
            new("MeanReversion", corr.MeanReversion, QuantScoreCalculator.MeanReversionMax, 1.0, finalWeights.MeanReversion),
            new("PriceMomentum", corr.PriceMomentum, QuantScoreCalculator.PriceMomentumMax, 1.0, finalWeights.PriceMomentum),
            new("RelativeStrength", corr.RelativeStrength, QuantScoreCalculator.RelativeStrengthMax, 1.0, finalWeights.RelativeStrength),
            new("InsiderPurchase", corr.InsiderPurchase, QuantScoreCalculator.InsiderPurchaseMax, 1.0, finalWeights.InsiderPurchase),
            new("EarningsSurprise", corr.EarningsSurprise, QuantScoreCalculator.EarningsSurpriseMax, 1.0, finalWeights.EarningsSurprise)
        };

        return new BacktestReport(
            components, currentSignalStats, recalibratedSignalStats, finalWeights,
            allSamplesByHorizon[horizonTradingDays].Count, outOfSampleCount, walkForwardSteps,
            tickerCount, skipped, horizonTradingDays, DateTime.UtcNow);
    }

    private async Task<IReadOnlyList<PriceBar>?> FetchBarsAsync(string ticker, CancellationToken ct)
    {
        try
        {
            var history = await stockAnalysis.GetHistoryAsync(ticker, "5y", ct);
            return history?.Bars;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Distinct, chronologically-sorted Form-4 insider Purchase filing dates for a ticker -
    /// a best-effort fetch (a network/parse failure here degrades to "no known purchases", same as a
    /// ticker with genuinely no insider Purchase filings, rather than failing the whole backtest run).</summary>
    private async Task<string[]> FetchInsiderPurchaseDatesAsync(string ticker, CancellationToken ct)
    {
        try
        {
            var insider = await stockAnalysis.GetInsiderAsync(ticker, ct);
            return insider?.Transactions
                .Where(t => t.TransactionType == "Purchase" && t.Date is not null)
                .Select(t => t.Date!)
                .Distinct()
                .OrderBy(d => d, StringComparer.Ordinal)
                .ToArray() ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Distinct, chronologically-sorted (reported-quarter date, EPS surprise%) pairs for a
    /// ticker - a best-effort fetch (mirrors FetchInsiderPurchaseDatesAsync's degrade-to-empty
    /// pattern on a network/parse failure). Yahoo's earningsHistory module typically covers only the
    /// last ~4 reported quarters (roughly 1 year), so bars older than that simply get no earnings
    /// signal (EarningsSurpriseSignal(null, null) => 0), the same "missing data -> neutral" pattern
    /// already used for Ma200/BollingerBands on early bars and for InsiderPurchase's ~2y coverage cap.</summary>
    private async Task<(string Date, double SurprisePercent)[]> FetchEarningsSurpriseEventsAsync(string ticker, CancellationToken ct)
    {
        try
        {
            var earnings = await stockAnalysis.GetEarningsAsync(ticker, ct);
            return earnings?.History
                .Where(h => h.Date.Length > 0 && h.SurprisePercent is not null)
                .Select(h => (h.Date, h.SurprisePercent!.Value))
                .Distinct()
                .OrderBy(e => e.Date, StringComparer.Ordinal)
                .ToArray() ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Splits one chronologically-ordered list into `folds` contiguous, (roughly) equal,
    /// non-overlapping chunks covering the whole input - chunk 0 is the oldest, chunk `folds-1` the
    /// most recent. Generic (not tied to Sample) so the chunking logic itself is directly
    /// unit-testable without needing real backtest samples.</summary>
    public static List<T>[] ChunkChronologically<T>(IReadOnlyList<T> items, int folds)
    {
        var chunks = new List<T>[folds];
        for (var c = 0; c < folds; c++)
        {
            var start = (int)((long)items.Count * c / folds);
            var end = (int)((long)items.Count * (c + 1) / folds);
            chunks[c] = items.Skip(start).Take(Math.Max(0, end - start)).ToList();
        }
        return chunks;
    }

    /// <summary>Elementwise mean of one or more recalibrated weight sets - how per-horizon fits are
    /// pooled into the single weight set actually offered for Apply/auto-apply. Equal-weights the
    /// horizons that produced a fit (a horizon skipped for lack of training data simply doesn't
    /// contribute, rather than being counted as a zero).</summary>
    public static QuantScoreCalculator.Weights AverageWeights(IReadOnlyList<QuantScoreCalculator.Weights> weights)
    {
        if (weights.Count == 0) return QuantScoreCalculator.Weights.Default;
        return new QuantScoreCalculator.Weights(
            Trend: weights.Average(w => w.Trend),
            Momentum: weights.Average(w => w.Momentum),
            Macd: weights.Average(w => w.Macd),
            Vol: weights.Average(w => w.Vol),
            MeanReversion: weights.Average(w => w.MeanReversion),
            PriceMomentum: weights.Average(w => w.PriceMomentum),
            RelativeStrength: weights.Average(w => w.RelativeStrength),
            InsiderPurchase: weights.Average(w => w.InsiderPurchase),
            EarningsSurprise: weights.Average(w => w.EarningsSurprise));
    }

    /// <summary>Fits all eight components jointly by OLS against excess (vs <see cref="BenchmarkTicker"/>)
    /// forward returns (see LinearRegression) and recalibrates weights from the resulting coefficients'
    /// magnitudes - reused both per walk-forward training window and for the final full-dataset fit.
    /// Also computes each component's independent Pearson correlation, purely for ComponentStat display
    /// alongside the regression-derived weight.</summary>
    private static (QuantScoreCalculator.Weights Weights, ComponentCorrelations Correlations) FitWeights(IReadOnlyList<Sample> samples)
    {
        var excessReturns = samples.Select(s => s.ExcessReturnPct).ToList();

        var trendCorr = PearsonCorrelation(samples.Select(s => s.Trend).ToList(), excessReturns);
        var momentumCorr = PearsonCorrelation(samples.Select(s => s.Momentum).ToList(), excessReturns);
        var macdCorr = PearsonCorrelation(samples.Select(s => s.Macd).ToList(), excessReturns);
        var volCorr = PearsonCorrelation(samples.Select(s => s.Vol).ToList(), excessReturns);
        var meanReversionCorr = PearsonCorrelation(samples.Select(s => s.MeanReversion).ToList(), excessReturns);
        var priceMomentumCorr = PearsonCorrelation(samples.Select(s => s.PriceMomentum).ToList(), excessReturns);
        var relativeStrengthCorr = PearsonCorrelation(samples.Select(s => s.RelativeStrength).ToList(), excessReturns);
        var insiderPurchaseCorr = PearsonCorrelation(samples.Select(s => s.InsiderPurchase).ToList(), excessReturns);
        var earningsSurpriseCorr = PearsonCorrelation(samples.Select(s => s.EarningsSurprise).ToList(), excessReturns);

        double[] coefficients;
        if (samples.Count > 20)
        {
            var features = samples
                .Select(s => new[]
                {
                    s.Trend, s.Momentum, s.Macd, s.Vol, s.MeanReversion, s.PriceMomentum,
                    s.RelativeStrength, s.InsiderPurchase, s.EarningsSurprise
                })
                .ToArray();
            // LinearRegression's default ridge (1e-6) is meaningless once X^T X's diagonal reaches the
            // tens of thousands (a full-universe backtest has hundreds of thousands of samples) -
            // several of these technical signals move together (Trend/PriceMomentum/MeanReversion all
            // track price direction to some degree), and with essentially no regularization at this
            // scale that collinearity showed up exactly as OLS theory predicts: Macd received an
            // implausibly large coefficient (2x+) relative to its near-zero marginal correlation,
            // while Momentum (a visibly larger marginal correlation) got pushed toward zero - an
            // unstable fit artifact, not a real finding. Tried ridge=n*0.01 first (still unstable,
            // Macd stayed inflated); ridge=n*1.0 is what actually stabilized the coefficients into
            // something consistent with the marginal correlations' relative ordering.
            coefficients = LinearRegression.FitCoefficients(features, excessReturns, ridge: samples.Count * 1.0);
        }
        else
        {
            coefficients = new double[9]; // too little data to fit meaningfully - RecalibrateWeights treats all-zero as "no signal"
        }

        var weights = RecalibrateWeights(
            coefficients[0], coefficients[1], coefficients[2], coefficients[3],
            coefficients[4], coefficients[5], coefficients[6], coefficients[7], coefficients[8]);
        return (weights, new ComponentCorrelations(trendCorr, momentumCorr, macdCorr, volCorr, meanReversionCorr, priceMomentumCorr, relativeStrengthCorr, insiderPurchaseCorr, earningsSurpriseCorr));
    }

    public readonly record struct ComponentCorrelations(
        double Trend, double Momentum, double Macd, double Vol,
        double MeanReversion, double PriceMomentum, double RelativeStrength, double InsiderPurchase,
        double EarningsSurprise);

    /// <summary>Redistributes the fixed point budget across components in proportion to the
    /// magnitude of each one's regression coefficient (or correlation, for callers that pass those
    /// instead - the math is identical either way) - a component that adds twice the explanatory
    /// power of another ends up counting twice as much, regardless of its original point scale. Uses
    /// |value| (magnitude only): a surprising negative coefficient says a component's direction may be
    /// historically backwards, which recalibrating the weight alone can't fix (that's a "the rule
    /// itself may be wrong" finding, not a "counts for more/less" finding) - reported via
    /// ComponentStat.Correlation for the caller to notice and investigate, not silently flipped
    /// here.</summary>
    public static QuantScoreCalculator.Weights RecalibrateWeights(
        double trend, double momentum, double macd, double vol,
        double meanReversion, double priceMomentum, double relativeStrength, double insiderPurchase,
        double earningsSurprise)
    {
        var magnitudes = new[]
        {
            Math.Abs(trend), Math.Abs(momentum), Math.Abs(macd),
            Math.Abs(vol), Math.Abs(meanReversion), Math.Abs(priceMomentum), Math.Abs(relativeStrength),
            Math.Abs(insiderPurchase), Math.Abs(earningsSurprise)
        };
        var totalMagnitude = magnitudes.Sum();

        // No signal at all (e.g. too little data) - leave weights unchanged rather than divide by zero.
        if (totalMagnitude < 1e-9) return QuantScoreCalculator.Weights.Default;

        double DesiredWeight(double magnitude, double currentMax) =>
            (TotalBudget * (magnitude / totalMagnitude)) / currentMax;

        return new QuantScoreCalculator.Weights(
            Trend: DesiredWeight(magnitudes[0], QuantScoreCalculator.TrendMax),
            Momentum: DesiredWeight(magnitudes[1], QuantScoreCalculator.MomentumMax),
            Macd: DesiredWeight(magnitudes[2], QuantScoreCalculator.MacdMax),
            Vol: DesiredWeight(magnitudes[3], QuantScoreCalculator.VolMax),
            MeanReversion: DesiredWeight(magnitudes[4], QuantScoreCalculator.MeanReversionMax),
            PriceMomentum: DesiredWeight(magnitudes[5], QuantScoreCalculator.PriceMomentumMax),
            RelativeStrength: DesiredWeight(magnitudes[6], QuantScoreCalculator.RelativeStrengthMax),
            InsiderPurchase: DesiredWeight(magnitudes[7], QuantScoreCalculator.InsiderPurchaseMax),
            EarningsSurprise: DesiredWeight(magnitudes[8], QuantScoreCalculator.EarningsSurpriseMax));
    }

    /// <summary>Buckets samples by the Signal their eight backtestable components alone would have
    /// produced under the given weights (Sentiment is excluded entirely here, not assumed neutral -
    /// there's no historical news to plug in, so this measures the technical/insider components in
    /// isolation rather than pretending sentiment contributed zero).</summary>
    private static IReadOnlyList<SignalStats> BucketBySignal(IReadOnlyList<Sample> samples, QuantScoreCalculator.Weights weights)
    {
        // Sample.Trend/Momentum/etc are raw [-1,1] (or [0,1] for InsiderPurchase) signals
        // (QuantScoreCalculator.*Signal outputs) - must scale by each component's point budget before
        // summing, exactly like Calculate() does, or the resulting "score" is far too small to ever
        // cross Buy/HoldThreshold.
        var scores = samples.Select(s =>
            s.Trend * QuantScoreCalculator.TrendMax * weights.Trend
            + s.Momentum * QuantScoreCalculator.MomentumMax * weights.Momentum
            + s.Macd * QuantScoreCalculator.MacdMax * weights.Macd
            + s.Vol * QuantScoreCalculator.VolMax * weights.Vol
            + s.MeanReversion * QuantScoreCalculator.MeanReversionMax * weights.MeanReversion
            + s.PriceMomentum * QuantScoreCalculator.PriceMomentumMax * weights.PriceMomentum
            + s.RelativeStrength * QuantScoreCalculator.RelativeStrengthMax * weights.RelativeStrength
            + s.InsiderPurchase * QuantScoreCalculator.InsiderPurchaseMax * weights.InsiderPurchase
            + s.EarningsSurprise * QuantScoreCalculator.EarningsSurpriseMax * weights.EarningsSurprise);
        var returns = samples.Select(s => s.ExcessReturnPct);
        return BucketBySignal(scores.ToList(), returns.ToList());
    }

    /// <summary>Merges per-fold SignalStats (from multiple walk-forward out-of-sample test chunks)
    /// into one aggregate per Signal - counts sum, average return and hit rate are count-weighted so
    /// a fold with more samples counts proportionally more.</summary>
    public static IReadOnlyList<SignalStats> MergeSignalStats(IReadOnlyList<IReadOnlyList<SignalStats>> perFold)
    {
        return perFold.SelectMany(f => f)
            .GroupBy(s => s.Signal)
            .Select(g =>
            {
                var totalCount = g.Sum(s => s.Count);
                var avgReturn = totalCount > 0 ? g.Sum(s => s.AvgExcessReturnPct * s.Count) / totalCount : 0.0;
                var withHitRate = g.Where(s => s.HitRatePct is not null).ToList();
                var hitRateCount = withHitRate.Sum(s => s.Count);
                double? hitRate = hitRateCount > 0
                    ? withHitRate.Sum(s => s.HitRatePct!.Value * s.Count) / hitRateCount
                    : null;
                return new SignalStats(g.Key, totalCount, avgReturn, hitRate);
            })
            .OrderBy(s => s.Signal)
            .ToList();
    }

    /// <summary>A ticker's forward return over the window (closeNow -&gt; closeFuture) minus the
    /// benchmark's return over the identical dates (benchNow -&gt; benchFuture) - the label every
    /// correlation/regression/Buy-Avoid-outcome in this engine is measured against, per the class
    /// remarks on why raw (non-benchmark-relative) forward return isn't used. Pulled out as a pure,
    /// directly unit-testable function rather than left inline in RunAsync's per-bar loop, matching
    /// BucketBySignal/RecalibrateWeights/PearsonCorrelation below - RunAsync's bug history (see class
    /// remarks) is entirely bugs that lived only in code reachable exclusively through a full
    /// network-backed run.</summary>
    public static double ExcessReturnPct(double closeNow, double closeFuture, double benchNow, double benchFuture)
    {
        var ownReturnPct = (closeFuture - closeNow) / closeNow * 100;
        var benchmarkReturnPct = (benchFuture - benchNow) / benchNow * 100;
        return ownReturnPct - benchmarkReturnPct;
    }

    /// <summary>Buckets pre-computed (score, excess-return) pairs by the Signal each score would
    /// have produced, using the same thresholds QuantScoreCalculator.Calculate applies live.
    /// Decoupled from Sample/Weights so it's directly unit-testable with synthetic data rather than
    /// only reachable through a full network-backed RunAsync.</summary>
    public static IReadOnlyList<SignalStats> BucketBySignal(IReadOnlyList<double> scores, IReadOnlyList<double> excessReturnsPct)
    {
        var buckets = new Dictionary<Signal, List<double>>
        {
            [Signal.Buy] = [], [Signal.Hold] = [], [Signal.Avoid] = []
        };

        for (var i = 0; i < scores.Count; i++)
        {
            var signal = scores[i] > QuantScoreCalculator.BuyThreshold ? Signal.Buy
                : scores[i] > QuantScoreCalculator.HoldThreshold ? Signal.Hold
                : Signal.Avoid;
            buckets[signal].Add(excessReturnsPct[i]);
        }

        return buckets.Select(kv =>
        {
            var (signal, returns) = (kv.Key, kv.Value);
            var avg = returns.Count > 0 ? returns.Average() : 0.0;
            double? hitRate = signal switch
            {
                Signal.Buy when returns.Count > 0 => returns.Count(r => r > 0) / (double)returns.Count * 100,
                Signal.Avoid when returns.Count > 0 => returns.Count(r => r < 0) / (double)returns.Count * 100,
                _ => null
            };
            return new SignalStats(signal, returns.Count, avg, hitRate);
        }).OrderBy(s => s.Signal).ToList();
    }

    public static double PearsonCorrelation(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        var n = x.Count;
        if (n < 2) return 0.0;

        var meanX = x.Average();
        var meanY = y.Average();
        double covXy = 0, varX = 0, varY = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = x[i] - meanX;
            var dy = y[i] - meanY;
            covXy += dx * dy;
            varX += dx * dx;
            varY += dy * dy;
        }

        if (varX == 0 || varY == 0) return 0.0;
        return covXy / Math.Sqrt(varX * varY);
    }
}
