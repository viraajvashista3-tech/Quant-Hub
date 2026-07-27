using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.Styling;

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

    /// <summary>Swaps the app's theme variant - Avalonia re-resolves every DynamicResource /
    /// ThemeDictionaries lookup (Colors.axaml) automatically from this one property, and
    /// FluentAvalonia's own control chrome follows the same variant. Call once at startup after the
    /// Application is constructed, and automatically again whenever Theme changes.</summary>
    public void ApplyTheme()
    {
        Application.Current!.RequestedThemeVariant =
            Theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        ApplyAccent();
    }

    /// <summary>Applies the currently-selected accent to both the app's own PrimaryBrush resource
    /// and FluentAvalonia's native accent ramp, so any FluentAvalonia-supplied chrome not
    /// explicitly re-skinned by the app's own styles still follows the same accent color. Re-applied
    /// on every theme swap too, so switching Dark/Light never resets the chosen accent.</summary>
    public void ApplyAccent()
    {
        var color = ParseHsl(GetAccent().Hsl);
        Application.Current!.Resources["PrimaryBrush"] = new SolidColorBrush(color);

        if (Application.Current!.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault() is { } fluentTheme)
        {
            fluentTheme.CustomAccentColor = color;
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

    /// <summary>Parses the "H S% L%" HSL triples AccentColors is defined in (e.g. "190 100% 50%")
    /// into an Avalonia Color - avoids needing a second, RGB-hex copy of the same six accents.</summary>
    private static Color ParseHsl(string hsl)
    {
        var parts = hsl.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var h = double.Parse(parts[0]);
        var s = double.Parse(parts[1].TrimEnd('%')) / 100.0;
        var l = double.Parse(parts[2].TrimEnd('%')) / 100.0;

        if (s == 0)
        {
            var gray = (byte)Math.Round(l * 255);
            return Color.FromRgb(gray, gray, gray);
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        var hk = h / 360.0;
        var r = HueToRgb(p, q, hk + 1.0 / 3);
        var g = HueToRgb(p, q, hk);
        var b = HueToRgb(p, q, hk - 1.0 / 3);
        return Color.FromRgb((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
}
