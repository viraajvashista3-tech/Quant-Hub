using System.IO;
using System.Text.Json;

namespace QuantHub.Desktop.Services;

/// <summary>Persists a user-curated list of tickers as JSON under
/// %LOCALAPPDATA%\QuantHub\watchlist.json, mirroring ScoreWeightsService's Load/Save pattern. Starts
/// empty - no seeded default tickers.</summary>
public sealed class WatchlistService
{
    private readonly string _path;
    private List<string> _tickers;

    public event EventHandler? Changed;

    public WatchlistService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuantHub"))
    {
    }

    /// <summary>Lets callers (tests) point persistence at a directory other than the real
    /// %LOCALAPPDATA%\QuantHub, so exercising Add/Remove/Load doesn't touch the user's actual
    /// watchlist file.</summary>
    public WatchlistService(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "watchlist.json");
        _tickers = Load();
    }

    public IReadOnlyList<string> Tickers => _tickers;

    public void Add(string ticker)
    {
        var upper = ticker.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(upper) || _tickers.Contains(upper)) return;
        _tickers.Add(upper);
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(string ticker)
    {
        if (!_tickers.Remove(ticker.ToUpperInvariant())) return;
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private List<string> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                if (JsonSerializer.Deserialize<List<string>>(json) is { } loaded) return loaded;
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
            File.WriteAllText(_path, JsonSerializer.Serialize(_tickers, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // best-effort persistence; not fatal if it fails
        }
    }
}
