namespace Fixon.Domain.Penalties;

/// <summary>
/// Контракт для детерминированного расчёта штрафа.
/// Domain-first: не сохраняет и не логирует.
/// </summary>
public interface IPenaltyCalculator
{
    PenaltyCalculationOutput Calculate(PenaltyDefinition definition, PenaltyCalculationInput input);
}

