using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace QuantHub.Desktop.Services;

/// <summary>Three genuinely distinct experience tiers - Beginner still sees the chart (just
/// simplified, plain-English framing), Intermediate adds the full technical chart/metrics,
/// Pro adds the deep breakdown/reasoning cards.</summary>
public enum ViewMode
{
    Beginner,
    Intermediate,
    Pro
}

public enum AppTheme
{
    Dark,
    Light
}

public sealed record AccentColor(string Name, string Hsl, string Label);

public sealed class AppSettings
{
    public ViewMode ViewMode { get; set; } = ViewMode.Intermediate;
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public string AccentName { get; set; } = "cyan";
    public string ClaudeApiKey { get; set; } = "";
}

/// <summary>Native replacement for ProModeContext/ThemeContext + localStorage: persists view mode,
/// theme, and accent color as JSON under %LOCALAPPDATA%\QuantHub\settings.json. Both ViewMode and
/// Theme are observable so pages/brushes react instantly, same as the web app's context-driven
/// re-render.</summary>
public sealed partial class SettingsService : ObservableObject
{
    public static readonly IReadOnlyList<AccentColor> AccentColors =
    [
        new AccentColor("cyan", "190 100% 50%", "Cyan"),
        new AccentColor("green", "142 70% 45%", "Green"),
        new AccentColor("amber", "40 90% 55%", "Amber"),
        new AccentColor("violet", "270 80% 60%", "Violet"),
        new AccentColor("rose", "345 85% 60%", "Rose"),
        new AccentColor("blue", "210 90% 55%", "Blue")
    ];

    private static readonly Dictionary<string, string> DarkPalette = new()
    {
        ["BackgroundBrush"] = "#0A0B10",
        ["SurfaceBrush"] = "#0F1117",
        ["PanelBorderBrush"] = "#2A2F3A",
        ["TextBrush"] = "#E6E8EC",
        ["MutedTextBrush"] = "#8B93A1"
    };

    private static readonly Dictionary<string, string> LightPalette = new()
    {
        ["BackgroundBrush"] = "#F3F4F6",
        ["SurfaceBrush"] = "#FFFFFF",
        ["PanelBorderBrush"] = "#E2E5EA",
        ["TextBrush"] = "#15171C",
        ["MutedTextBrush"] = "#6B7280"
    };

    private readonly string _path;

    [ObservableProperty]
    private ViewMode _viewMode;

    [ObservableProperty]
    private AppTheme _theme;

    [ObservableProperty]
    private string _claudeApiKey = "";

    public string AccentName { get; set; }

    public SettingsService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuantHub");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");

        var loaded = Load();
        _viewMode = loaded.ViewMode;
        _theme = loaded.Theme;
        AccentName = loaded.AccentName;
        _claudeApiKey = loaded.ClaudeApiKey;
    }

    partial void OnViewModeChanged(ViewMode value) => Save();

    partial void OnThemeChanged(AppTheme value)
    {
        ApplyTheme();
        Save();
    }

    partial void OnClaudeApiKeyChanged(string value) => Save();

    /// <summary>Swaps the surface/text brushes application-wide. Call once at startup after the
    /// window is constructed, and automatically again whenever Theme changes.</summary>
    public void ApplyTheme()
    {
        var palette = Theme == AppTheme.Dark ? DarkPalette : LightPalette;
        foreach (var (key, hex) in palette)
        {
            Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                if (JsonSerializer.Deserialize<AppSettings>(json) is { } loaded) return loaded;
            }
        }
        catch
        {
            // corrupt or unreadable settings file - fall back to defaults rather than crash startup
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var current = new AppSettings { ViewMode = ViewMode, Theme = Theme, AccentName = AccentName, ClaudeApiKey = ClaudeApiKey };
            var json = JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // best-effort persistence; not fatal if it fails (e.g. locked file, disk full)
        }
    }

    public AccentColor GetAccent() => AccentColors.FirstOrDefault(a => a.Name == AccentName) ?? AccentColors[0];

    public bool IsPro => ViewMode == ViewMode.Pro;
    public bool IsAtLeastIntermediate => ViewMode != ViewMode.Beginner;

    public void CycleViewMode()
    {
        ViewMode = (ViewMode)(((int)ViewMode + 1) % 3);
    }
}
