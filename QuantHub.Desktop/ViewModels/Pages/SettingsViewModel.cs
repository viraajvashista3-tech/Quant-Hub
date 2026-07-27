using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuantHub.Desktop.Services;

namespace QuantHub.Desktop.ViewModels.Pages;

public sealed record ViewModeOption(ViewMode Mode, string Label, string Description);

public sealed record ThemeOption(AppTheme Theme, string Label);

/// <summary>Settings page - theme (dark/light) and view-mode (Beginner/Intermediate/Pro)
/// selection, pinned below the sidebar nav. Both write straight through to SettingsService,
/// which persists and applies them immediately.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;

    public IReadOnlyList<ViewModeOption> ViewModeOptions { get; } =
    [
        new(ViewMode.Beginner, "Beginner", "Plain-English guidance with a simplified chart - no jargon, no raw numbers."),
        new(ViewMode.Intermediate, "Intermediate", "The full technical chart, RSI, and key metrics."),
        new(ViewMode.Pro, "Pro", "Everything in Intermediate plus the score breakdown, Bollinger Bands, and detailed reasoning.")
    ];

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(AppTheme.Dark, "Dark"),
        new(AppTheme.Light, "Light")
    ];

    public SettingsViewModel(SettingsService settings)
    {
        _settings = settings;
        _settings.PropertyChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.ViewMode)) OnPropertyChanged(nameof(ViewMode));
        if (e.PropertyName == nameof(SettingsService.Theme)) OnPropertyChanged(nameof(Theme));
        if (e.PropertyName == nameof(SettingsService.ClaudeApiKey)) OnPropertyChanged(nameof(ClaudeApiKey));
    }

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

    public string ClaudeApiKey
    {
        get => _settings.ClaudeApiKey;
        set => _settings.ClaudeApiKey = value;
    }

    [RelayCommand]
    private void SelectViewMode(ViewModeOption option) => ViewMode = option.Mode;

    [RelayCommand]
    private void SelectTheme(ThemeOption option) => Theme = option.Theme;
}
