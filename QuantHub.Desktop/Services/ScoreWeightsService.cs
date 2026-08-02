using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using QuantHub.Core.Analysis;

namespace QuantHub.Desktop.Services;

/// <summary>Persists the recalibrated QuantScoreCalculator.Weights (see AutoBacktestService) as JSON
/// under %LOCALAPPDATA%\QuantHub\weights.json, mirroring SettingsService's persistence pattern.
/// Starts at QuantScoreCalculator.Weights.Default (i.e. today's hand-picked scoring, unchanged)
/// until a backtest is run and its recalibration explicitly applied. Observable so pages already
/// showing a QuantScore (Terminal) can reload the instant new weights are applied, the same way
/// they already react to SettingsService.ViewMode changing.</summary>
public sealed partial class ScoreWeightsService : ObservableObject
{
    private readonly string _path;

    [ObservableProperty]
    private QuantScoreCalculator.Weights _current;

    public ScoreWeightsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuantHub"))
    {
    }

    /// <summary>Lets callers (tests) point persistence at a directory other than the real
    /// %LOCALAPPDATA%\QuantHub, so exercising Apply/Reset/Load doesn't touch the user's actual
    /// weights file.</summary>
    public ScoreWeightsService(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "weights.json");
        _current = Load();
    }

    public void Apply(QuantScoreCalculator.Weights weights)
    {
        Current = weights;
        Save();
    }

    public void Reset() => Apply(QuantScoreCalculator.Weights.Default);

    private QuantScoreCalculator.Weights Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                if (JsonSerializer.Deserialize<QuantScoreCalculator.Weights>(json) is { } loaded) return loaded;
            }
        }
        catch
        {
            // corrupt or unreadable file - fall back to defaults rather than crash startup
        }
        return QuantScoreCalculator.Weights.Default;
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // best-effort persistence; not fatal if it fails
        }
    }
}
