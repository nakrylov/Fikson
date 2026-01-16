using Fixon.Domain.Abstractions;
using Fixon.Domain.Financial;

namespace Fixon.Domain.Penalties;

/// <summary>
/// Ручная корректировка штрафа (override/adjustment) (docs/07).
/// Исходный penalty не изменяется; итог определяется последней корректировкой.
/// </summary>
public sealed class PenaltyAdjustment : TenantEntity
{
    public Guid ClaimId { get; private set; }

    public decimal OriginalPenaltyAmount { get; private set; }
    public decimal NewPenaltyAmount { get; private set; }
    public string Currency { get; private set; } = null!;

    public string Reason { get; private set; } = null!;
    public Guid AdjustedByUserId { get; private set; }
    public DateTimeOffset AdjustedAt { get; private set; }

    private PenaltyAdjustment() { } // EF

    public PenaltyAdjustment(
        Guid id,
        Guid companyId,
        Guid claimId,
        Money originalPenalty,
        Money newPenalty,
        string reason,
        Guid adjustedByUserId,
        DateTimeOffset adjustedAt)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException(nameof(reason));
        if (!string.Equals(originalPenalty.Currency.Value, newPenalty.Currency.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Currency mismatch in penalty adjustment.");
        }

        Id = id;
        CompanyId = companyId;
        ClaimId = claimId;
        OriginalPenaltyAmount = originalPenalty.Amount;
        NewPenaltyAmount = newPenalty.Amount;
        Currency = newPenalty.Currency.Value;
        Reason = reason;
        AdjustedByUserId = adjustedByUserId;
        AdjustedAt = adjustedAt;
    }
}

