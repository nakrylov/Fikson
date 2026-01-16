using System.Globalization;
using System.Text.Json;

namespace Fixon.Domain.Sla.Engine;

/// <summary>
/// Парсер JSON полей из SlaRuleVersion (AppliesWhenJson / ConditionJson / PenaltyJson).
/// Должен быть толерантен к форме JSON (массив или объект с "conditions"), чтобы не фиксировать схему без docs.
/// </summary>
public static class SlaRuleJsonParser
{
    public static SlaRuleDefinition Parse(string appliesWhenJson, string conditionJson, string penaltyJson)
    {
        var when = ParseWhenConditions(appliesWhenJson);
        var then = ParseThenConditions(conditionJson);

        // PenaltyJson в v1 хранится как jsonb и интерпретируется на уровне приложения/отчётности.
        // Engine не применяет штрафы (docs: engine не зависит от инфраструктуры и не делает сайд-эффекты).
        return new SlaRuleDefinition(when, then, penaltyJson);
    }

    private static IReadOnlyList<WhenCondition> ParseWhenConditions(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = ExtractConditionsArray(doc.RootElement);
        var result = new List<WhenCondition>(root.GetArrayLength());

        foreach (var item in root.EnumerateArray())
        {
            var attribute = GetRequiredString(item, "attribute", "Attribute");
            var opRaw = GetRequiredString(item, "operator", "Operator");
            var value = GetRequiredString(item, "value", "Value");

            if (!Enum.TryParse<WhenOperator>(opRaw, ignoreCase: true, out var op))
            {
                throw new FormatException($"Unknown When operator: '{opRaw}'.");
            }

            result.Add(new WhenCondition(attribute, op, value));
        }

        return result;
    }

    private static IReadOnlyList<ThenCondition> ParseThenConditions(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = ExtractConditionsArray(doc.RootElement);
        var result = new List<ThenCondition>(root.GetArrayLength());

        foreach (var item in root.EnumerateArray())
        {
            var factKey = GetRequiredString(item, "factKey", "FactKey");
            var opRaw = GetRequiredString(item, "operator", "Operator");
            var expectedValue = GetRequiredString(item, "expectedValue", "ExpectedValue");
            var unit = GetOptionalString(item, "unit", "Unit");

            if (!Enum.TryParse<ThenOperator>(opRaw, ignoreCase: true, out var op))
            {
                throw new FormatException($"Unknown Then operator: '{opRaw}'.");
            }

            result.Add(new ThenCondition(factKey, op, expectedValue, unit));
        }

        return result;
    }

    private static JsonElement ExtractConditionsArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("conditions", out var conditions) && conditions.ValueKind == JsonValueKind.Array)
            {
                return conditions;
            }
        }

        throw new FormatException("Expected JSON array or object with 'conditions' array.");
    }

    private static string GetRequiredString(JsonElement obj, string camelCaseName, string pascalCaseName)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("Condition item must be an object.");
        }

        if (TryGetString(obj, camelCaseName, out var value) || TryGetString(obj, pascalCaseName, out value))
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new FormatException($"Field '{camelCaseName}' is required.");
            }

            return value;
        }

        throw new FormatException($"Field '{camelCaseName}' is required.");
    }

    private static string? GetOptionalString(JsonElement obj, string camelCaseName, string pascalCaseName)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryGetString(obj, camelCaseName, out var value) || TryGetString(obj, pascalCaseName, out value))
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private static bool TryGetString(JsonElement obj, string propertyName, out string? value)
    {
        if (obj.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
            {
                value = prop.GetString();
                return true;
            }

            // Допускаем числа/булевы как строку (чтобы не фиксировать схему без docs)
            if (prop.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                value = prop.GetRawText();
                return true;
            }
        }

        value = null;
        return false;
    }
}

