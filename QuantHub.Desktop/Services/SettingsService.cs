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

public sealed record ViewModeOption(ViewMode Mode, string Label, string Description);

/// <summary>Which page the Shell should show on launch. LastViewed (the default) means "wherever
/// I was when I closed the app" - the three fixed options are for anyone who always wants to land
/// on the same page regardless of where they left off.</summary>
public enum StartupPage
{
    LastViewed,
    Terminal,
    Universe,
    TrackRecord
}

public sealed record StartupPageOption(StartupPage Page, string Label);

/// <summary>How often the Shell should silently re-run the current page's RefreshCommand - for
/// anyone who leaves the app open on a second monitor and wants it to stay current on its own.
/// Off (the default) matches the app's previous, always-manual-refresh behavior exactly.</summary>
public enum AutoRefreshInterval
{
    Off,
    OneMinute,
    FiveMinutes,
    FifteenMinutes
}

public sealed record AutoRefreshOption(AutoRefreshInterval Interval, string Label);

public sealed class AppSettings
{
    public ViewMode ViewMode { get; set; } = ViewMode.Intermediate;
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public string AccentName { get; set; } = "cyan";
    public string LastTicker { get; set; } = "AAPL";
    public StartupPage StartupPage { get; set; } = StartupPage.LastViewed;
    public string LastViewedNavTag { get; set; } = "Terminal";
    public AutoRefreshInterval AutoRefreshInterval { get; set; } = AutoRefreshInterval.Off;
    public bool AlwaysOnTop { get; set; }
}

/// <summary>Native replacement for ProModeContext/ThemeContext + localStorage: persists view mode,
/// theme, and accent color as JSON under %LOCALAPPDATA%\QuantHub\settings.json. Both ViewMode and
/// Theme are observable so pages/brushes react instantly, same as the web app's context-driven
/// re-render.</summary>
public sealed partial class SettingsService : ObservableObject
{
    // Concrete List<T>, not a `[...]` collection expression: collection expressions targeting an
    // interface type (IReadOnlyList<T> here) get backed by the compiler's `<>z__ReadOnlyArray<T>`,
    // which has no reflectable `Item` indexer property - Avalonia's `{Binding Foo[n]}` XAML syntax
    // resolves that to null instead of throwing, so a bound CommandParameter silently becomes null.
    // List<T> has a real Item property and binds correctly.
    public static readonly IReadOnlyList<AccentColor> AccentColors = new List<AccentColor>
    {
        new("cyan", "190 100% 50%", "Cyan"),
        new("green", "142 70% 45%", "Green"),
        new("amber", "40 90% 55%", "Amber"),
        new("violet", "270 80% 60%", "Violet"),
        new("rose", "345 85% 60%", "Rose"),
        new("blue", "210 90% 55%", "Blue")
    };

    public static readonly IReadOnlyList<ViewModeOption> ViewModeOptions = new List<ViewModeOption>
    {
        new(ViewMode.Beginner, "Beginner", "Plain-English guidance with a simplified chart - no jargon, no raw numbers."),
        new(ViewMode.Intermediate, "Intermediate", "The full technical chart, RSI, and key metrics."),
        new(ViewMode.Pro, "Pro", "Everything in Intermediate plus the score breakdown, Bollinger Bands, and detailed reasoning.")
    };

    public static readonly IReadOnlyList<StartupPageOption> StartupPageOptions = new List<StartupPageOption>
    {
        new(StartupPage.LastViewed, "Last Viewed"),
        new(StartupPage.Terminal, "Terminal"),
        new(StartupPage.Universe, "Universe"),
        new(StartupPage.TrackRecord, "Track Record")
    };

    public static readonly IReadOnlyList<AutoRefreshOption> AutoRefreshOptions = new List<AutoRefreshOption>
    {
        new(AutoRefreshInterval.Off, "Off"),
        new(AutoRefreshInterval.OneMinute, "1 min"),
        new(AutoRefreshInterval.FiveMinutes, "5 min"),
        new(AutoRefreshInterval.FifteenMinutes, "15 min")
    };

    /// <summary>Pure mapping, pulled out for direct unit testing - Timeout.InfiniteTimeSpan for Off
    /// since callers (ShellViewModel) stop/never start a timer for that case anyway, but a total
    /// mapping is simpler to reason about than one with a "shouldn't be called" gap.</summary>
    public static TimeSpan ToTimeSpan(AutoRefreshInterval interval) => interval switch
    {
        AutoRefreshInterval.OneMinute => TimeSpan.FromMinutes(1),
        AutoRefreshInterval.FiveMinutes => TimeSpan.FromMinutes(5),
        AutoRefreshInterval.FifteenMinutes => TimeSpan.FromMinutes(15),
        _ => Timeout.InfiniteTimeSpan
    };

    private readonly string _path;

    [ObservableProperty]
    private ViewMode _viewMode;

    [ObservableProperty]
    private AppTheme _theme;

    public string AccentName { get; set; }

    /// <summary>The ticker AppState.ActiveTicker initializes from at startup, and is kept in sync
    /// with it thereafter - so the app reopens on whatever you were last looking at instead of
    /// always resetting to AAPL. Plain property (not [ObservableProperty]) since AppState, the only
    /// thing that changes it, already has its own ActiveTicker change notification; nothing needs a
    /// second one from here.</summary>
    public string LastTicker { get; set; }

    public StartupPage StartupPage { get; set; }

    /// <summary>Updated on every nav change regardless of StartupPage, so switching StartupPage to
    /// LastViewed later doesn't require first revisiting a page for it to have a value.</summary>
    public string LastViewedNavTag { get; set; }

    [ObservableProperty]
    private AutoRefreshInterval _autoRefreshInterval;

    [ObservableProperty]
    private bool _alwaysOnTop;

    public SettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuantHub"))
    {
    }

    /// <summary>Lets callers (tests) point persistence at a directory other than the real
    /// %LOCALAPPDATA%\QuantHub, so exercising this doesn't touch a real machine's settings file -
    /// same pattern as ScoreWeightsService/UpdateCheckService's test-friendly constructors.</summary>
    public SettingsService(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "settings.json");

        var loaded = Load();
        _viewMode = loaded.ViewMode;
        _theme = loaded.Theme;
        AccentName = loaded.AccentName;
        LastTicker = loaded.LastTicker;
        StartupPage = loaded.StartupPage;
        LastViewedNavTag = loaded.LastViewedNavTag;
        _autoRefreshInterval = loaded.AutoRefreshInterval;
        _alwaysOnTop = loaded.AlwaysOnTop;
    }

    partial void OnViewModeChanged(ViewMode value) => Save();

    partial void OnAutoRefreshIntervalChanged(AutoRefreshInterval value) => Save();

    partial void OnAlwaysOnTopChanged(bool value) => Save();

    partial void OnThemeChanged(AppTheme value)
    {
        ApplyTheme();
        Save();
    }

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
            var current = new AppSettings
            {
                ViewMode = ViewMode,
                Theme = Theme,
                AccentName = AccentName,
                LastTicker = LastTicker,
                StartupPage = StartupPage,
                LastViewedNavTag = LastViewedNavTag,
                AutoRefreshInterval = AutoRefreshInterval,
                AlwaysOnTop = AlwaysOnTop
            };
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
    /// into an Avalonia Color - avoids needing a second, RGB-hex copy of the same six accents.
    /// Internal (not private) so the Settings page's accent-swatch converter can reuse the exact same
    /// parsing instead of duplicating it.</summary>
    internal static Color ParseHsl(string hsl)
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
