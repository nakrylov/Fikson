namespace Fixon.Domain.Sla.Engine;

/// <summary>
/// Доменное представление SLA правила для вычисления.
/// Источник: поля JSON в SlaRuleVersion (см. docs/03-sla-rule-model.md, раздел 11).
/// </summary>
public sealed record SlaRuleDefinition(
    IReadOnlyList<WhenCondition> WhenConditions,
    IReadOnlyList<ThenCondition> ThenConditions,
    string PenaltyJson);

/// <summary>
/// Утилита, связывающая domain entity SlaRuleVersion с domain engine.
/// Engine не читает из БД: Application передаёт SlaRuleVersion и контекст/факты явно.
/// </summary>
public static class SlaRuleDefinitionFactory
{
    public static SlaRuleDefinition FromRuleVersion(SlaRuleVersion ruleVersion)
    {
        return SlaRuleJsonParser.Parse(ruleVersion.AppliesWhenJson, ruleVersion.ConditionJson, ruleVersion.PenaltyJson);
    }
}

