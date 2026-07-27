using System.Text.Json;

namespace QuantHub.Core.Yahoo;

/// <summary>
/// Yahoo's quoteSummary API wraps most numeric fields as {"raw": 1.23, "fmt": "1.23"} but leaves
/// some plain (module-dependent) - these helpers handle both shapes uniformly.
/// </summary>
public static class YahooJson
{
    public static double? Raw(JsonElement result, string module, string field)
    {
        if (!result.TryGetProperty(module, out var mod) || mod.ValueKind != JsonValueKind.Object) return null;
        if (!mod.TryGetProperty(field, out var el)) return null;
        return ExtractRaw(el);
    }

    public static long? RawLong(JsonElement result, string module, string field)
    {
        var d = Raw(result, module, field);
        return d is { } v ? (long)v : null;
    }

    public static string? Str(JsonElement result, string module, string field)
    {
        if (!result.TryGetProperty(module, out var mod) || mod.ValueKind != JsonValueKind.Object) return null;
        if (!mod.TryGetProperty(field, out var el)) return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }

    public static double? RawAny(JsonElement result, IEnumerable<string> modules, string field)
    {
        foreach (var m in modules)
        {
            var v = Raw(result, m, field);
            if (v is not null) return v;
        }
        return null;
    }

    public static string? StrAny(JsonElement result, IEnumerable<string> modules, string field)
    {
        foreach (var m in modules)
        {
            var v = Str(result, m, field);
            if (v is not null) return v;
        }
        return null;
    }

    public static JsonElement? Array(JsonElement result, string module, string field)
    {
        if (!result.TryGetProperty(module, out var mod) || mod.ValueKind != JsonValueKind.Object) return null;
        if (!mod.TryGetProperty(field, out var el) || el.ValueKind != JsonValueKind.Array) return null;
        return el;
    }

    private static double? ExtractRaw(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Number => el.GetDouble(),
        JsonValueKind.Object when el.TryGetProperty("raw", out var raw) && raw.ValueKind == JsonValueKind.Number => raw.GetDouble(),
        _ => null
    };
}
