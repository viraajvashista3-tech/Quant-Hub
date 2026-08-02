using System.IO;
using System.Text.Json;
using QuantHub.Core.Backtesting;
using QuantHub.Core.Models;
using QuantHub.Core.Universe;

namespace QuantHub.Desktop.Services;

/// <summary>Keeps QuantScoreCalculator's weights recalibrated without requiring the user to remember
/// to visit the Backtest page. Checks once per app launch whether it's been at least a week since
/// the last run (5 years of price history doesn't shift meaningfully day-to-day, so daily reruns
/// would just burn network calls for no new information) and, if so, runs the same walk-forward
/// backtest the manual "Run Backtest" button does, in the background.
///
/// Auto-apply is gated by a safety check: a recalibration that looks clearly *worse* out-of-sample
/// than today's weights is left alone rather than silently adopted - see IsRecalibrationSafe. The
/// Backtest page surfaces LastRunUtc/LastReport regardless, so what happened (or didn't) is always
/// inspectable, not invisible.</summary>
public sealed class AutoBacktestService
{
    private static readonly TimeSpan RecheckInterval = TimeSpan.FromDays(7);
    private const int HorizonTradingDays = 10;

    private readonly BacktestEngine _engine;
    private readonly ScoreWeightsService _scoreWeights;
    private readonly string _statePath;

    public DateTime? LastRunUtc { get; private set; }
    public bool IsChecking { get; private set; }
    public BacktestReport? LastReport { get; private set; }

    public event EventHandler? StateChanged;

    public AutoBacktestService(BacktestEngine engine, ScoreWeightsService scoreWeights)
    {
        _engine = engine;
        _scoreWeights = scoreWeights;

        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuantHub");
        Directory.CreateDirectory(dir);
        _statePath = Path.Combine(dir, "autobacktest.json");
        var state = LoadState();
        LastRunUtc = state?.LastRunUtc;
        LastReport = state?.LastReport;
    }

    /// <summary>Fire-and-forget: call once at startup. No-ops if the last run is still within
    /// RecheckInterval.</summary>
    public void RunInBackgroundIfDue() => _ = RunIfDueAsync();

    private Task RunIfDueAsync() =>
        LastRunUtc is { } last && DateTime.UtcNow - last < RecheckInterval
            ? Task.CompletedTask
            : RunNowAsync();

    /// <summary>Also callable directly (e.g. a "Check Now" button) to force an out-of-schedule run.</summary>
    public async Task RunNowAsync()
    {
        IsChecking = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            var report = await _engine.RunAsync(UniverseData.AllTickers, HorizonTradingDays);
            LastReport = report;
            if (report.SampleCount > 0 && IsRecalibrationSafe(report))
            {
                _scoreWeights.Apply(report.RecalibratedWeights);
            }
            LastRunUtc = DateTime.UtcNow;
            SaveState();
        }
        catch
        {
            // best-effort - a network hiccup shouldn't crash startup or block the app
        }
        finally
        {
            IsChecking = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Refuses to auto-adopt a recalibration that looks clearly worse out-of-sample than
    /// today's weights on the signal that matters most (Buy hit rate). A small tolerance absorbs
    /// ordinary sample noise between runs; a real regression is left for the user to notice and
    /// decide on manually via the Backtest page rather than silently applied.</summary>
    private static bool IsRecalibrationSafe(BacktestReport report)
    {
        var current = report.CurrentSignalStats.FirstOrDefault(s => s.Signal == Signal.Buy);
        var recalibrated = report.RecalibratedSignalStats.FirstOrDefault(s => s.Signal == Signal.Buy);
        return current?.HitRatePct is not { } cur || recalibrated?.HitRatePct is not { } rec || rec >= cur - 2.0;
    }

    /// <summary>Persists the full report alongside the timestamp (not just LastRunUtc) so reopening
    /// the app days later still shows what the last automatic check actually found, rather than an
    /// empty page until the next scheduled run.</summary>
    private State? LoadState()
    {
        try
        {
            if (File.Exists(_statePath))
            {
                var json = File.ReadAllText(_statePath);
                return JsonSerializer.Deserialize<State>(json);
            }
        }
        catch
        {
            // corrupt or unreadable file - treat as "never run" rather than crash startup
        }
        return null;
    }

    private void SaveState()
    {
        try
        {
            File.WriteAllText(_statePath, JsonSerializer.Serialize(new State(LastRunUtc, LastReport)));
        }
        catch
        {
            // best-effort persistence; not fatal if it fails
        }
    }

    private sealed record State(DateTime? LastRunUtc, BacktestReport? LastReport);
}
