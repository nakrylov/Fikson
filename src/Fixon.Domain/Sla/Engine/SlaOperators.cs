namespace Fixon.Domain.Sla.Engine;

/// <summary>
/// Операторы When-условий (см. docs/03-sla-rule-model.md).
/// </summary>
public enum WhenOperator
{
    EQUALS = 1,
    NOT_EQUALS = 2,
    IN = 3,
    NOT_IN = 4,
}

/// <summary>
/// Операторы Then-условий (см. docs/03-sla-rule-model.md).
/// </summary>
public enum ThenOperator
{
    EQUALS = 1,
    NOT_EQUALS = 2,
    GREATER_THAN = 3,
    GREATER_OR_EQUALS = 4,
    LESS_THAN = 5,
    LESS_OR_EQUALS = 6,
}

