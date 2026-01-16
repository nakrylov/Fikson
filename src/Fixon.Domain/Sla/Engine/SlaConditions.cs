namespace Fixon.Domain.Sla.Engine;

/// <summary>
/// WhenCondition: Attribute + Operator + Value (см. docs/03-sla-rule-model.md).
/// When-условия объединяются через AND.
/// </summary>
public sealed record WhenCondition(
    string Attribute,
    WhenOperator Operator,
    string Value);

/// <summary>
/// ThenCondition: FactKey + Operator + ExpectedValue + Unit? (см. docs/03-sla-rule-model.md).
/// Then-условия объединяются через AND.
/// </summary>
public sealed record ThenCondition(
    string FactKey,
    ThenOperator Operator,
    string ExpectedValue,
    string? Unit);

