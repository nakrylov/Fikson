using System.Globalization;
using System.Text.Json;
using Fixon.Domain.Financial;

namespace Fixon.Domain.Penalties;

/// <summary>
/// Penalty calculator v1 по docs/07-penalties-financial-impact.md.
/// Детерминирован, audit-friendly (возвращает explain JSON).
/// </summary>
public sealed class PenaltyCalculatorV1 : IPenaltyCalculator
{
    private const int MoneyScale = 2; // docs/07: rounding до 2 знаков

    public PenaltyCalculationOutput Calculate(PenaltyDefinition definition, PenaltyCalculationInput input)
    {
        // Threshold: если параметр <= threshold → penalty = 0
        if (!input.Parameters.TryGetValue(definition.TriggerParameter, out var triggerValue))
        {
            throw new InvalidOperationException($"TriggerParameter '{definition.TriggerParameter}' not provided.");
        }

        if (definition.Threshold.HasValue && triggerValue <= definition.Threshold.Value)
        {
            return new PenaltyCalculationOutput(
                new Money(0m, definition.Currency),
                ExplanationJson(triggerValue, 0m, "below_threshold", definition, input));
        }

        var raw = definition.PenaltyType switch
        {
            PenaltyType.Fixed => definition.Value,
            PenaltyType.Percent => CalculatePercent(definition, input),
            PenaltyType.TimeBased => CalculateTimeBased(definition, triggerValue),
            _ => throw new ArgumentOutOfRangeException(nameof(definition.PenaltyType))
        };

        // min/max caps
        if (definition.MinPenalty.HasValue) raw = Math.Max(raw, definition.MinPenalty.Value);
        if (definition.MaxPenalty.HasValue) raw = Math.Min(raw, definition.MaxPenalty.Value);

        // rounding
        var rounded = ApplyRounding(raw, definition.Rounding);

        return new PenaltyCalculationOutput(
            new Money(rounded, definition.Currency),
            ExplanationJson(triggerValue, rounded, "calculated", definition, input));
    }

    private static decimal CalculatePercent(PenaltyDefinition def, PenaltyCalculationInput input)
    {
        if (def.BaseAmountSource is null)
        {
            throw new InvalidOperationException("BaseAmountSource is required for Percent penalty.");
        }

        var baseAmount = def.BaseAmountSource.Value switch
        {
            BaseAmountSource.ContractValue => input.ContractValue,
            BaseAmountSource.ShipmentValue => input.ShipmentValue,
            BaseAmountSource.Custom => input.CustomBaseAmount,
            _ => throw new ArgumentOutOfRangeException()
        };

        if (baseAmount is null)
        {
            throw new InvalidOperationException($"Base amount '{def.BaseAmountSource}' not provided.");
        }

        if (!string.Equals(baseAmount.Value.Currency.Value, def.Currency.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Currency conversion is not supported.");
        }

        return baseAmount.Value.Amount * (def.Value / 100m);
    }

    private static decimal CalculateTimeBased(PenaltyDefinition def, decimal triggerValue)
    {
        // TimeBased: Value = ставка за единицу времени.
        // В docs/07 единица времени не нормализована отдельным полем, поэтому triggerValue трактуем как "количество единиц".
        return def.Value * triggerValue;
    }

    private static decimal ApplyRounding(decimal amount, PenaltyRounding rounding)
    {
        return rounding switch
        {
            PenaltyRounding.None => decimal.Round(amount, MoneyScale, MidpointRounding.ToZero),
            PenaltyRounding.Floor => decimal.Round(Math.Floor(amount * 100m) / 100m, MoneyScale),
            PenaltyRounding.Ceil => decimal.Round(Math.Ceiling(amount * 100m) / 100m, MoneyScale),
            PenaltyRounding.Round => decimal.Round(amount, MoneyScale, MidpointRounding.AwayFromZero),
            _ => throw new ArgumentOutOfRangeException(nameof(rounding), rounding, null)
        };
    }

    private static string ExplanationJson(
        decimal triggerValue,
        decimal amount,
        string reason,
        PenaltyDefinition def,
        PenaltyCalculationInput input)
    {
        var obj = new Dictionary<string, object?>
        {
            ["reason"] = reason,
            ["penaltyType"] = def.PenaltyType.ToString(),
            ["triggerParameter"] = def.TriggerParameter,
            ["triggerValue"] = triggerValue.ToString(CultureInfo.InvariantCulture),
            ["threshold"] = def.Threshold?.ToString(CultureInfo.InvariantCulture),
            ["value"] = def.Value.ToString(CultureInfo.InvariantCulture),
            ["currency"] = def.Currency.Value,
            ["baseAmountSource"] = def.BaseAmountSource?.ToString(),
            ["minPenalty"] = def.MinPenalty?.ToString(CultureInfo.InvariantCulture),
            ["maxPenalty"] = def.MaxPenalty?.ToString(CultureInfo.InvariantCulture),
            ["rounding"] = def.Rounding.ToString(),
            ["amount"] = amount.ToString(CultureInfo.InvariantCulture),
        };

        return JsonSerializer.Serialize(obj);
    }
}

