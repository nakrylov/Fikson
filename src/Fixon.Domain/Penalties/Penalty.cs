using Fixon.Domain.Abstractions;
using Fixon.Domain.Financial;

namespace Fixon.Domain.Penalties;

/// <summary>
/// Penalty — рассчитанное денежное последствие для Claim.
/// Инвариант: один Claim имеет ровно один Penalty (docs/07).
/// </summary>
public sealed class Penalty : TenantEntity
{
    public Guid ClaimId { get; private set; }

    public decimal CalculatedAmount { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTimeOffset CalculatedAt { get; private set; }

    /// <summary>
    /// Snapshot penalty json на момент расчёта (audit-safe).
    /// </summary>
    public string PenaltyJsonSnapshot { get; private set; } = null!;

    /// <summary>
    /// Explain JSON: почему именно такая сумма (audit-friendly).
    /// </summary>
    public string ExplanationJson { get; private set; } = null!;

    private Penalty() { } // EF

    public Penalty(
        Guid claimId,
        Guid companyId,
        Money amount,
        DateTimeOffset calculatedAt,
        string penaltyJsonSnapshot,
        string explanationJson)
    {
        if (claimId == Guid.Empty) throw new ArgumentException(nameof(claimId));
        if (string.IsNullOrWhiteSpace(penaltyJsonSnapshot)) throw new ArgumentException(nameof(penaltyJsonSnapshot));
        if (string.IsNullOrWhiteSpace(explanationJson)) throw new ArgumentException(nameof(explanationJson));

        ClaimId = claimId;
        CompanyId = companyId;
        CalculatedAmount = amount.Amount;
        Currency = amount.Currency.Value;
        CalculatedAt = calculatedAt;
        PenaltyJsonSnapshot = penaltyJsonSnapshot;
        ExplanationJson = explanationJson;
    }
}

