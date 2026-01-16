using Fixon.Domain.Financial;

namespace Fixon.Domain.Penalties;

/// <summary>
/// Вход для расчёта штрафа по breach/evaluation.
/// Содержит только детерминированные данные, подготовленные Application layer.
/// </summary>
public sealed record PenaltyCalculationInput(
    Guid CompanyId,
    Guid ContractVersionId,
    Guid SlaRuleVersionId,
    Guid SlaEvaluationId,
    DateTimeOffset EvaluatedAtUtc,
    IReadOnlyDictionary<string, decimal> Parameters,
    Money? ContractValue,
    Money? ShipmentValue,
    Money? CustomBaseAmount);

/// <summary>
/// Результат расчёта штрафа: сумма + explain JSON (audit-friendly).
/// Amount может быть 0.
/// </summary>
public sealed record PenaltyCalculationOutput(
    Money Amount,
    string ExplanationJson);

