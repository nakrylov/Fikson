using System.Collections.Generic;

namespace Fixon.Domain.Sla.Engine;

/// <summary>
/// Расширенный результат вычисления для домена engine.
/// В persistence (SlaEvaluation) в v1 фиксируется только Pass/Fail (см. docs/03-sla-rule-model.md).
/// NotApplicable возвращается engine'ом, а решение о сохранении остаётся за Application layer.
/// </summary>
public enum SlaEngineOutcome
{
    NotApplicable = 0,
    Pass = 1,
    Fail = 2,
}

/// <summary>
/// Входные данные для вычисления SLA.
/// Application layer подготавливает факты и контекстные атрибуты (engine ничего не читает из БД).
/// </summary>
public sealed record SlaEvaluationInput(
    IReadOnlyDictionary<string, string?> ContextAttributes,
    IReadOnlyDictionary<string, string?> FactValues,
    DateTimeOffset EvaluatedAtUtc);

/// <summary>
/// Результат вычисления SLA правила.
/// CalculatedValuesJson - детерминированное JSON (ключи сортируются).
/// </summary>
public sealed record SlaEvaluationOutput(
    SlaEngineOutcome Outcome,
    string CalculatedValuesJson);

