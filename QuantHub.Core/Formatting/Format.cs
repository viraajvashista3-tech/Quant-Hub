using System.Globalization;

namespace QuantHub.Core.Formatting;

public static class Format
{
    public static string LargeNumber(double? num)
    {
        if (num is not { } n) return "-";
        var abs = Math.Abs(n);
        if (abs >= 1.0e12) return (n / 1.0e12).ToString("F2", CultureInfo.InvariantCulture) + "T";
        if (abs >= 1.0e9) return (n / 1.0e9).ToString("F2", CultureInfo.InvariantCulture) + "B";
        if (abs >= 1.0e6) return (n / 1.0e6).ToString("F2", CultureInfo.InvariantCulture) + "M";
        // toLocaleString(undefined, { maximumFractionDigits: 2 }) shows UP TO 2 decimals without
        // padding whole numbers - "#,##0.##" matches that (unlike "N2", which always pads to 2dp).
        return n.ToString("#,##0.##", CultureInfo.InvariantCulture);
    }

    public static string Percent(double? num)
    {
        if (num is not { } n) return "-";
        var sign = n > 0 ? "+" : "";
        return sign + n.ToString("F2", CultureInfo.InvariantCulture) + "%";
    }

    public static string Currency(double? num)
    {
        if (num is not { } n) return "-";
        return "$" + n.ToString("F2", CultureInfo.InvariantCulture);
    }

    public enum ValueType
    {
        Currency,
        Percent,
        Number
    }

    /// <summary>
    /// Mirrors formatValue's "percent" branch, which unlike <see cref="Percent"/> never prepends "+" for positive values.
    /// </summary>
    public static string Value(double? num, ValueType type = ValueType.Number)
    {
        if (num is not { } n) return "-";
        return type switch
        {
            ValueType.Currency => Currency(n),
            ValueType.Percent => n.ToString("F2", CultureInfo.InvariantCulture) + "%",
            ValueType.Number => LargeNumber(n),
            _ => LargeNumber(n)
        };
    }
}
