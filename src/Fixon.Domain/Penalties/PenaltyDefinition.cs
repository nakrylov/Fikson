using System.Text.Json;
using Fixon.Domain.Financial;

namespace Fixon.Domain.Penalties;

/// <summary>
/// Penalty definition, описанная в docs/07-penalties-financial-impact.md (PenaltyJson schema).
/// Хранит исходный JSON (для аудитируемости) + распарсенную структуру.
/// </summary>
public sealed record PenaltyDefinition(
    PenaltyType PenaltyType,
    decimal Value,
    CurrencyCode Currency,
    PenaltyRounding Rounding,
    BaseAmountSource? BaseAmountSource,
    string TriggerParameter,
    decimal? Threshold,
    decimal? MinPenalty,
    decimal? MaxPenalty,
    string RawJson)
{
    public static PenaltyDefinition FromJson(string penaltyJson)
    {
        using var doc = JsonDocument.Parse(penaltyJson);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("PenaltyJson must be an object.");
        }

        var penaltyType = ParseEnum<PenaltyType>(root, "PenaltyType");
        var value = ParseDecimal(root, "Value");
        var currency = new CurrencyCode(ParseString(root, "Currency"));
        var rounding = ParseEnum<PenaltyRounding>(root, "Rounding", defaultValue: PenaltyRounding.None);

        BaseAmountSource? baseAmountSource = null;
        if (root.TryGetProperty("BaseAmountSource", out _))
        {
            baseAmountSource = ParseEnum<BaseAmountSource>(root, "BaseAmountSource");
        }

        var triggerParameter = ParseString(root, "TriggerParameter");
        var threshold = TryParseDecimal(root, "Threshold");
        var minPenalty = TryParseDecimal(root, "MinPenalty");
        var maxPenalty = TryParseDecimal(root, "MaxPenalty");

        return new PenaltyDefinition(
            penaltyType,
            value,
            currency,
            rounding,
            baseAmountSource,
            triggerParameter,
            threshold,
            minPenalty,
            maxPenalty,
            penaltyJson);
    }

    private static string ParseString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"PenaltyJson.{name} is required and must be a string.");
        }
        var s = prop.GetString();
        if (string.IsNullOrWhiteSpace(s)) throw new FormatException($"PenaltyJson.{name} is required.");
        return s;
    }

    private static decimal ParseDecimal(JsonElement root, string name)
    {
        var raw = ParseRaw(root, name);
        if (!decimal.TryParse(raw, out var d))
        {
            throw new FormatException($"PenaltyJson.{name} must be a decimal.");
        }
        return d;
    }

    private static decimal? TryParseDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var raw = prop.ValueKind == JsonValueKind.Number ? prop.GetRawText() : prop.ToString();
        if (!decimal.TryParse(raw, out var d))
        {
            throw new FormatException($"PenaltyJson.{name} must be a decimal.");
        }
        return d;
    }

    private static string ParseRaw(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
        {
            throw new FormatException($"PenaltyJson.{name} is required.");
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.String => prop.GetString() ?? string.Empty,
            _ => prop.ToString()
        };
    }

    private static TEnum ParseEnum<TEnum>(JsonElement root, string name, TEnum? defaultValue = null)
        where TEnum : struct
    {
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
        {
            if (defaultValue.HasValue) return defaultValue.Value;
            throw new FormatException($"PenaltyJson.{name} is required.");
        }

        var s = prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.GetRawText();
        if (string.IsNullOrWhiteSpace(s))
        {
            if (defaultValue.HasValue) return defaultValue.Value;
            throw new FormatException($"PenaltyJson.{name} is required.");
        }

        if (!Enum.TryParse<TEnum>(s, ignoreCase: true, out var e))
        {
            throw new FormatException($"PenaltyJson.{name} has unknown value '{s}'.");
        }
        return e;
    }
}

