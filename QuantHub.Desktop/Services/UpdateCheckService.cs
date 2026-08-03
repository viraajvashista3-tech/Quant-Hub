using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace QuantHub.Desktop.Services;

public sealed record UpdateCheckResult(bool IsUpdateAvailable, string LatestVersion, string ReleaseUrl);

/// <summary>Checks GitHub Releases once a day for a tag newer than the version currently running, so
/// someone who installed an early build actually finds out later improvements exist instead of being
/// silently stuck on whatever they first downloaded. Mirrors ScoreWeightsService/AutoBacktestService's
/// %LOCALAPPDATA%\QuantHub persistence pattern. Best-effort and silent on any failure (offline,
/// GitHub API rate-limited, private/renamed repo) - never blocks or interrupts startup; "no update
/// found" is a perfectly fine degrade mode, not an error worth surfacing to the user.</summary>
public sealed class UpdateCheckService
{
    private const string ApiUrl = "https://api.github.com/repos/viraajvashista3-tech/Quant-Hub/releases/latest";
    private static readonly TimeSpan RecheckInterval = TimeSpan.FromDays(1);

    private readonly HttpClient _http;
    private readonly string _path;
    private PersistedState? _state;

    private sealed record PersistedState(DateTime LastCheckUtc, string LatestTag, string ReleaseUrl);

    public UpdateCheckResult? Current { get; private set; }

    public event EventHandler? Updated;

    public UpdateCheckService(HttpClient http)
        : this(http, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuantHub"))
    {
    }

    /// <summary>Lets callers (tests) point persistence at a directory other than the real
    /// %LOCALAPPDATA%\QuantHub, so exercising this doesn't touch a real machine's state file -
    /// same pattern as ScoreWeightsService's test-friendly constructor.</summary>
    public UpdateCheckService(HttpClient http, string dataDirectory)
    {
        _http = http;
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "updatecheck.json");
        _state = Load(_path);
        Current = BuildResult(_state);
    }

    public void RunInBackgroundIfDue() => _ = RunIfDueAsync();

    private Task RunIfDueAsync() =>
        _state is { } s && DateTime.UtcNow - s.LastCheckUtc < RecheckInterval
            ? Task.CompletedTask
            : RunNowAsync();

    public async Task RunNowAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, ApiUrl);
            // GitHub's API rejects requests with no User-Agent header.
            req.Headers.UserAgent.ParseAdd("QuantTerminal-UpdateCheck");
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStreamAsync(ct));
            var tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var url = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : null;
            if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(url)) return;

            RecordCheckResult(tag, url);
        }
        catch
        {
            // best-effort - see class doc.
        }
    }

    /// <summary>The non-network part of a successful check: persists the result and recomputes
    /// Current. Pulled out so it's directly unit-testable without mocking HttpClient - no other
    /// external API client in this codebase does that either (see YahooFinanceClient), so this stays
    /// consistent with the project's existing testing boundary. Public rather than internal since
    /// QuantHub.Desktop has no InternalsVisibleTo declared for the test project (unlike
    /// QuantHub.Core).</summary>
    public void RecordCheckResult(string latestTag, string releaseUrl)
    {
        _state = new PersistedState(DateTime.UtcNow, latestTag, releaseUrl);
        Save(_path, _state);
        Current = BuildResult(_state);
        Updated?.Invoke(this, EventArgs.Empty);
    }

    private static UpdateCheckResult? BuildResult(PersistedState? state)
    {
        if (state is null) return null;
        var runningVersion = typeof(UpdateCheckService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        return new UpdateCheckResult(
            IsNewerVersion(runningVersion, state.LatestTag),
            state.LatestTag.TrimStart('v', 'V'),
            state.ReleaseUrl);
    }

    /// <summary>Normalizes both sides to Major.Minor.Build before comparing, so a 4-part assembly
    /// version (e.g. "1.0.0.0") and a 3-part release tag (e.g. "v1.2.0") compare unambiguously.
    /// Returns false (not an update) for anything that doesn't parse as a plain numeric version - a
    /// pre-release/test tag like "v0.0.1-test" should never trigger an update prompt.</summary>
    public static bool IsNewerVersion(string currentVersionText, string latestTag)
    {
        if (!Version.TryParse(currentVersionText, out var currentFull)) return false;
        var current = new Version(currentFull.Major, currentFull.Minor, Math.Max(currentFull.Build, 0));

        if (!Version.TryParse(latestTag.TrimStart('v', 'V'), out var latestFull)) return false;
        var latest = new Version(latestFull.Major, latestFull.Minor, Math.Max(latestFull.Build, 0));

        return latest > current;
    }

    private static PersistedState? Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(path)) : null;
        }
        catch
        {
            return null;
        }
    }

    private static void Save(string path, PersistedState state)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // best-effort persistence; not fatal if it fails
        }
    }
}
