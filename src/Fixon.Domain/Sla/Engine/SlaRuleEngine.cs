using System.Globalization;

namespace Fixon.Domain.Sla.Engine;

/// <summary>
/// Детерминированный SLA rule engine.
/// - Не знает про EF/DbContext/HTTP
/// - Не использует DateTime.Now (EvaluatedAt передаётся во входе)
/// - Не сохраняет результаты (Application сохраняет SlaEvaluation/Claim)
///
/// Семантика v1 (см. docs/03-sla-rule-model.md):
/// - When: AND; если не выполнено → NotApplicable (и нарушение не фиксируется)
/// - Then: AND; если хотя бы одно Then не выполнено → Fail, иначе Pass
/// </summary>
public static class SlaRuleEngine
{
    /*
     * Пример сценария (согласован с docs/03-sla-rule-model.md по семантике):
     *
     * When:
     *   cargo.category EQUALS Vegetables
     * Then:
     *   waiting_time_minutes LESS_OR_EQUALS 30
     *
     * Вход:
     *   ContextAttributes: { "cargo.category": "Vegetables" }
     *   FactValues: { "waiting_time_minutes": "45" }
     *
     * Ожидаемый результат:
     *   Outcome = Fail (потому что When выполнен, а Then не выполнен)
     *
     * Если ContextAttributes: { "cargo.category": "Alcohol" }
     *   Outcome = NotApplicable (и приложение может не создавать SlaEvaluation запись)
     */
    public static SlaEvaluationOutput Evaluate(SlaRuleDefinition rule, SlaEvaluationInput input)
    {
        var calculated = new Dictionary<string, object?>
        {
            ["evaluatedAtUtc"] = input.EvaluatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
        };

        // WHEN (контекстные атрибуты)
        if (!AllWhenSatisfied(rule.WhenConditions, input.ContextAttributes, calculated))
        {
            calculated["outcome"] = "NotApplicable";
            return new SlaEvaluationOutput(SlaEngineOutcome.NotApplicable, DeterministicJson.SerializeSorted(calculated));
        }

        // THEN (факты)
        var thenOutcome = EvaluateThen(rule.ThenConditions, input.FactValues, calculated);
        calculated["outcome"] = thenOutcome.ToString();

        return new SlaEvaluationOutput(thenOutcome, DeterministicJson.SerializeSorted(calculated));
    }

    private static bool AllWhenSatisfied(
        IReadOnlyList<WhenCondition> conditions,
        IReadOnlyDictionary<string, string?> context,
        IDictionary<string, object?> calculated)
    {
        // AND по всем условиям
        foreach (var c in conditions)
        {
            if (!context.TryGetValue(c.Attribute, out var actual))
            {
                // Если контекстный атрибут отсутствует — правило не применимо (строгая трактовка).
                calculated["when.missingAttribute"] = c.Attribute;
                return false;
            }

            if (!EvaluateWhenCondition(actual, c))
            {
                calculated["when.failedAttribute"] = c.Attribute;
                calculated["when.actual"] = actual;
                calculated["when.expected"] = c.Value;
                calculated["when.operator"] = c.Operator.ToString();
                return false;
            }
        }

        return true;
    }

    private static SlaEngineOutcome EvaluateThen(
        IReadOnlyList<ThenCondition> conditions,
        IReadOnlyDictionary<string, string?> facts,
        IDictionary<string, object?> calculated)
    {
        // AND по всем условиям; Fail если хотя бы одно Then не выполнено.
        foreach (var c in conditions)
        {
            if (!facts.TryGetValue(c.FactKey, out var actual) || string.IsNullOrWhiteSpace(actual))
            {
                calculated["then.missingFactKey"] = c.FactKey;
                return SlaEngineOutcome.Fail;
            }

            if (!EvaluateThenCondition(actual, c, out var reason))
            {
                calculated["then.failedFactKey"] = c.FactKey;
                calculated["then.actual"] = actual;
                calculated["then.expected"] = c.ExpectedValue;
                calculated["then.operator"] = c.Operator.ToString();
                calculated["then.unit"] = c.Unit;
                calculated["then.reason"] = reason;
                return SlaEngineOutcome.Fail;
            }
        }

        return SlaEngineOutcome.Pass;
    }

    private static bool EvaluateWhenCondition(string? actual, WhenCondition c)
    {
        actual ??= string.Empty;
        var expected = c.Value;

        return c.Operator switch
        {
            WhenOperator.EQUALS => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            WhenOperator.NOT_EQUALS => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            WhenOperator.IN => SplitCsv(expected).Any(v => string.Equals(actual, v, StringComparison.OrdinalIgnoreCase)),
            WhenOperator.NOT_IN => SplitCsv(expected).All(v => !string.Equals(actual, v, StringComparison.OrdinalIgnoreCase)),
            _ => throw new ArgumentOutOfRangeException(nameof(c.Operator), c.Operator, "Unknown when operator")
        };
    }

    private static bool EvaluateThenCondition(string actualRaw, ThenCondition c, out string? reason)
    {
        reason = null;
        var expectedRaw = c.ExpectedValue;

        // EQUALS / NOT_EQUALS допускают строковое сравнение (включая true/false).
        if (c.Operator is ThenOperator.EQUALS or ThenOperator.NOT_EQUALS)
        {
            var equals = string.Equals(actualRaw, expectedRaw, StringComparison.OrdinalIgnoreCase);
            return c.Operator == ThenOperator.EQUALS ? equals : !equals;
        }

        // Остальные операторы требуют числового сравнения.
        if (!decimal.TryParse(actualRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var actual))
        {
            reason = "actual_not_numeric";
            return false;
        }

        if (!decimal.TryParse(expectedRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var expected))
        {
            reason = "expected_not_numeric";
            return false;
        }

        return c.Operator switch
        {
            ThenOperator.GREATER_THAN => actual > expected,
            ThenOperator.GREATER_OR_EQUALS => actual >= expected,
            ThenOperator.LESS_THAN => actual < expected,
            ThenOperator.LESS_OR_EQUALS => actual <= expected,
            _ => throw new ArgumentOutOfRangeException(nameof(c.Operator), c.Operator, "Unknown then operator")
        };
    }

    private static IEnumerable<string> SplitCsv(string csv)
    {
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

