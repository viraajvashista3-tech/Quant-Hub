using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using QuantHub.Desktop.Services;
using QuantHub.Desktop.Theming;

namespace QuantHub.Desktop.Converters;

/// <summary>Renders a SettingsService.AccentColor's "H S% L%" string as an actual swatch color, for
/// the Settings page's accent picker - reuses SettingsService.ParseHsl rather than a second copy of
/// the same HSL math.</summary>
public sealed class HslToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string hsl ? new SolidColorBrush(SettingsService.ParseHsl(hsl)) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps a SignalReason.Verdict string ("Positive"/"Negative"/"Warning"/"Neutral") to a brush.</summary>
public sealed class VerdictToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as string) switch
        {
            "Positive" => "PositiveBrush",
            "Negative" => "DestructiveBrush",
            "Warning" => "WarningBrush",
            _ => "MutedTextBrush"
        };
        return ThemeResources.GetBrush(key);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps a SignalReason.Verdict string to an emoji icon, matching the original's
/// CheckCircle (positive) / XCircle (negative) / AlertCircle (warning/neutral) treatment.</summary>
public sealed class VerdictToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => (value as string) switch
    {
        "Positive" => "✅",
        "Negative" => "❌",
        "Warning" => "⚠️",
        _ => "➖"
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps SentimentService.SentimentLabel values ("Bullish"/"Mildly Bullish"/"Neutral"/
/// "Mildly Bearish"/"Bearish") to a brush for the news-sentiment badge chip.</summary>
public sealed class NewsSentimentToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as string) switch
        {
            "Bullish" or "Mildly Bullish" => "PositiveBrush",
            "Bearish" or "Mildly Bearish" => "DestructiveBrush",
            _ => "MutedTextBrush"
        };
        return ThemeResources.GetBrush(key);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps InsiderAnalyzer's classified transaction type ("Purchase"/"Sale"/"Gift"/
/// "Option Exercise"/"Award/Grant"/"Unknown") to a brush for the insider transactions table.</summary>
public sealed class TransactionTypeToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as string) switch
        {
            "Purchase" => "PositiveBrush",
            "Sale" => "DestructiveBrush",
            _ => "MutedTextBrush"
        };
        return ThemeResources.GetBrush(key);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps Yahoo's insider transaction ownership code ("D"/"I") to a readable label.</summary>
public sealed class OwnershipCodeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => (value as string) switch
    {
        "D" => "Direct",
        "I" => "Indirect",
        var other => other
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps an AnalystData.ConsensusRating string to a brush - the same read
/// AnalystViewModel.ConsensusBrush already gives the Analyst page's own consensus badge, promoted to
/// a converter so the Watchlist and Universe Top 20 ranking tables can bind it directly from XAML too.</summary>
public sealed class ConsensusRatingToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as string) switch
        {
            "Strong Buy" or "Buy" => "PositiveBrush",
            "Hold" => "WarningBrush",
            "Sell" or "Strong Sell" => "DestructiveBrush",
            _ => "MutedTextBrush"
        };
        return ThemeResources.GetBrush(key);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps a numeric score contribution to green (positive) / red (negative) / muted (zero).</summary>
public sealed class SignToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var d = value switch
        {
            double dv => dv,
            _ => 0.0
        };
        var key = d > 0 ? "PositiveBrush" : d < 0 ? "DestructiveBrush" : "MutedTextBrush";
        return ThemeResources.GetBrush(key);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
