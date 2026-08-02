using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuantHub.Desktop.Services;

namespace QuantHub.Desktop.ViewModels.Pages;

public sealed record ThemeOption(AppTheme Theme, string Label);

/// <summary>Settings page - theme (dark/light) and view-mode (Beginner/Intermediate/Pro)
/// selection, pinned below the sidebar nav. Both write straight through to SettingsService,
/// which persists and applies them immediately.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly WatchlistService _watchlist;

    public IReadOnlyList<ViewModeOption> ViewModeOptions => SettingsService.ViewModeOptions;

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(AppTheme.Dark, "Dark"),
        new(AppTheme.Light, "Light")
    ];

    public IReadOnlyList<AccentColor> AccentColors => SettingsService.AccentColors;

    public SettingsViewModel(SettingsService settings, WatchlistService watchlist)
    {
        _settings = settings;
        _watchlist = watchlist;
        _settings.PropertyChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.ViewMode)) OnPropertyChanged(nameof(ViewMode));
        if (e.PropertyName == nameof(SettingsService.Theme)) OnPropertyChanged(nameof(Theme));
    }

    /// <summary>Not an [ObservableProperty]-backed SettingsService member (unlike ViewMode/Theme), so
    /// this page raises its own change notification after SelectAccent rather than relying on
    /// OnSettingsChanged above.</summary>
    public string AccentName => _settings.AccentName;

    public ViewMode ViewMode
    {
        get => _settings.ViewMode;
        set => _settings.ViewMode = value;
    }

    public AppTheme Theme
    {
        get => _settings.Theme;
        set => _settings.Theme = value;
    }

    [RelayCommand]
    private void SelectViewMode(ViewModeOption option) => ViewMode = option.Mode;

    [RelayCommand]
    private void SelectTheme(ThemeOption option) => Theme = option.Theme;

    [RelayCommand]
    private void SelectAccent(AccentColor accent)
    {
        _settings.AccentName = accent.Name;
        _settings.ApplyAccent();
        _settings.Save();
        OnPropertyChanged(nameof(AccentName));
    }

    // ---------- Watchlist backup ----------
    // Deliberately scoped to just the watchlist, not every local JSON file under
    // %LOCALAPPDATA%\QuantHub: it's the one piece of this app's state that's genuinely
    // irreplaceable if this machine is lost - weights/predictions/universe rankings all rebuild
    // themselves automatically.

    /// <summary>Byte-identical in shape to watchlist.json on disk - a plain, human-readable ticker
    /// array, not a bespoke format. Public static (pure) so the JSON round-trip is directly
    /// unit-testable without a real WatchlistService/temp directory.</summary>
    public static string BuildWatchlistExportJson(IReadOnlyList<string> tickers) =>
        JsonSerializer.Serialize(tickers, new JsonSerializerOptions { WriteIndented = true });

    /// <summary>Returns [] (not a thrown exception) for malformed input - the code-behind caller
    /// treats an empty result as "nothing to import" rather than needing its own try/catch around a
    /// parse failure.</summary>
    public static IReadOnlyList<string> ParseWatchlistImportJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public string ExportWatchlistJson() => BuildWatchlistExportJson(_watchlist.Tickers);

    /// <summary>Merges into the current watchlist (same dedupe-by-uppercase-symbol behavior as
    /// adding tickers one at a time from the Universe page - WatchlistService.Add already no-ops on
    /// a duplicate) rather than replacing it outright, so importing a backup can never silently
    /// discard tickers added since that backup was taken. Returns how many were actually new, for
    /// the caller to show a confirmation.</summary>
    public int ImportWatchlistJson(string json)
    {
        var before = _watchlist.Tickers.Count;
        foreach (var ticker in ParseWatchlistImportJson(json)) _watchlist.Add(ticker);
        return _watchlist.Tickers.Count - before;
    }
}
