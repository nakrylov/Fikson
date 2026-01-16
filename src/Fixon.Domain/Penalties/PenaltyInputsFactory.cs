using Fixon.Domain.Financial;
using Fixon.Domain.Sla;
using System.Text.Json;

namespace Fixon.Domain.Penalties;

/// <summary>
/// Связывает SLA evaluation output (CalculatedValuesJson) с penalty calculation input.
/// Domain-first: не читает БД, получает всё как параметры.
/// </summary>
public static class PenaltyInputsFactory
{
    public static PenaltyCalculationInput FromSlaEvaluation(
        Guid companyId,
        Guid contractVersionId,
        Guid slaRuleVersionId,
        Guid slaEvaluationId,
        DateTimeOffset evaluatedAtUtc,
        string calculatedValuesJson,
        Money? contractValue,
        Money? shipmentValue,
        Money? customBaseAmount)
    {
        var parameters = ParseNumericParameters(calculatedValuesJson);

        return new PenaltyCalculationInput(
            companyId,
            contractVersionId,
            slaRuleVersionId,
            slaEvaluationId,
            evaluatedAtUtc,
            parameters,
            contractValue,
            shipmentValue,
            customBaseAmount);
    }

    private static IReadOnlyDictionary<string, decimal> ParseNumericParameters(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, decimal>();
        }

        var dict = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            // Берём только числовые поля; остальные игнорируем (детерминированно).
            if (prop.Value.ValueKind == JsonValueKind.Number)
            {
                if (prop.Value.TryGetDecimal(out var d))
                {
                    dict[prop.Name] = d;
                }
            }
        }

        return dict;
    }
}

