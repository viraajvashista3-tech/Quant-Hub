using CommunityToolkit.Mvvm.ComponentModel;
using QuantHub.Core.Backtesting;
using QuantHub.Core.Models;
using QuantHub.Desktop.Services;

namespace QuantHub.Desktop.ViewModels.Pages;

public sealed record TrackRecordRow(Signal Signal, int Count, double AvgExcessReturnPct, double? HitRatePct);

/// <summary>The app's honesty page: the live, forward-tested track record for every signal type
/// (Buy/Hold/Avoid - not just Buy, unlike the sidebar's one-line badge), plus a plain-English
/// methodology explanation and an explicit statement that historical walk-forward testing has found
/// limited standalone predictive power in these components. This is the flagship transparency
/// surface for the app's "structured, honestly-tested research tool" framing - deliberately NOT a
/// resurrection of the old removed Backtest page's internal mechanics (weights, correlation tables,
/// Apply/Reset buttons), which stay exactly as automatic and hidden as they were before. This page
/// only ever shows outcomes that already happened, never the recalibration knobs themselves.</summary>
public sealed partial class TrackRecordViewModel : ObservableObject
{
    private readonly PredictionLogService _predictionLog;
    private readonly AutoBacktestService _autoBacktest;

    [ObservableProperty]
    private IReadOnlyList<TrackRecordRow> _rows = [];

    public bool HasData => Rows.Any(r => r.Count > 0);

    /// <summary>Confirms the automatic weekly recalibration is actually running, without exposing
    /// what it actually changed (the weight values/correlations stay internal) - "it's happening" is
    /// transparency; "here are the exact numbers to second-guess" is the mechanism this app has
    /// deliberately kept out of the user-facing surface.</summary>
    public string LastRecalibrationText => _autoBacktest.LastRunUtc is { } t
        ? $"Component weights were last automatically recalibrated {FormatAgo(t)}."
        : "Component weights haven't run an automatic recalibration check yet - this happens in the background, roughly weekly.";

    public TrackRecordViewModel(PredictionLogService predictionLog, AutoBacktestService autoBacktest)
    {
        _predictionLog = predictionLog;
        _autoBacktest = autoBacktest;
        _predictionLog.Updated += (_, _) => Rebuild();
        Rebuild();
    }

    private void Rebuild()
    {
        var stats = PredictionLog.ComputeStats(_predictionLog.Entries);
        Rows =
        [
            RowFor(stats, Signal.Buy),
            RowFor(stats, Signal.Hold),
            RowFor(stats, Signal.Avoid)
        ];
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(LastRecalibrationText));
    }

    private static TrackRecordRow RowFor(IReadOnlyList<SignalStats> stats, Signal signal)
    {
        var s = stats.FirstOrDefault(x => x.Signal == signal);
        return new TrackRecordRow(signal, s?.Count ?? 0, s?.AvgExcessReturnPct ?? 0, s?.HitRatePct);
    }

    private static string FormatAgo(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays} day{((int)span.TotalDays == 1 ? "" : "s")} ago";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours} hour{((int)span.TotalHours == 1 ? "" : "s")} ago";
        return "recently";
    }
}
