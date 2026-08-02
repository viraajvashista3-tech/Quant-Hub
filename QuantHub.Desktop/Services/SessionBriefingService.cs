using System.IO;
using System.Text.Json;
using QuantHub.Core.Models;

namespace QuantHub.Desktop.Services;

/// <summary>Persists the Signal (Buy/Hold/Avoid) last seen for each watchlisted ticker, so the next
/// time the watchlist loads it can tell the user what actually changed since they last looked -
/// "come back and see what's new" is the whole point, not just re-showing the same numbers. Persisted
/// as JSON under %LOCALAPPDATA%\QuantHub\lastsession.json, mirroring WatchlistService's Load/Save
/// pattern. Deliberately scoped to the watchlist only (not the full Universe sweep or every page) -
/// the watchlist is the user's own curated set, so a change there is the one most worth surfacing
/// proactively.</summary>
public sealed class SessionBriefingService
{
    private readonly string _path;
    private Dictionary<string, Signal> _lastSeen;

    public SessionBriefingService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuantHub"))
    {
    }

    /// <summary>Lets callers (tests) point persistence at a directory other than the real
    /// %LOCALAPPDATA%\QuantHub, mirroring WatchlistService/ScoreWeightsService's same test seam.</summary>
    public SessionBriefingService(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "lastsession.json");
        _lastSeen = Load();
    }

    /// <summary>Pure comparison: which currently-watchlisted tickers' Signal differs from what was
    /// last recorded, as ready-to-display sentences. A ticker with no prior recorded signal (new to
    /// the watchlist, or the very first run ever) is never reported - there's nothing to compare it
    /// against yet, which is different from "it changed."</summary>
    public static IReadOnlyList<string> DiffSignals(
        IReadOnlyDictionary<string, Signal> previous, IReadOnlyList<(string Ticker, string Name, Signal Signal)> current)
    {
        var messages = new List<string>();
        foreach (var (ticker, name, signal) in current)
        {
            if (previous.TryGetValue(ticker, out var prevSignal) && prevSignal != signal)
                messages.Add($"{ticker} ({name}) moved from {prevSignal} to {signal}.");
        }
        return messages;
    }

    /// <summary>Diffs the current watchlist signals against whatever was last recorded, then commits
    /// the current state as the new baseline before returning - so calling this again in the same
    /// sitting (e.g. two Refresh clicks) only ever reports genuinely new changes, never repeats one
    /// already shown.</summary>
    public IReadOnlyList<string> RecordAndDiff(IReadOnlyList<(string Ticker, string Name, Signal Signal)> current)
    {
        var messages = DiffSignals(_lastSeen, current);
        _lastSeen = current.ToDictionary(c => c.Ticker, c => c.Signal);
        Save();
        return messages;
    }

    private Dictionary<string, Signal> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                if (JsonSerializer.Deserialize<Dictionary<string, Signal>>(json) is { } loaded) return loaded;
            }
        }
        catch
        {
            // corrupt or unreadable file - start empty rather than crash startup
        }
        return [];
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(_lastSeen, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // best-effort persistence; not fatal if it fails
        }
    }
}
