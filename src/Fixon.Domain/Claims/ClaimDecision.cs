using Fixon.Domain.Abstractions;
using Fixon.Domain.Users;

namespace Fixon.Domain.Claims;

/// <summary>
/// Append-only решение по Claim.
/// </summary>
public sealed class ClaimDecision : TenantEntity
{
    public Guid ClaimId { get; private set; }
    public ClaimDecisionType DecisionType { get; private set; }
    public decimal DecisionAmount { get; private set; }
    public string Currency { get; private set; } = null!;
    public Guid DecidedByUserId { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }
    public string? Comment { get; private set; }

    public Claim? Claim { get; private set; }
    public User? DecidedByUser { get; private set; }

    private ClaimDecision() { } // EF

    public ClaimDecision(
        Guid id,
        Guid companyId,
        Guid claimId,
        ClaimDecisionType decisionType,
        decimal decisionAmount,
        string currency,
        Guid decidedByUserId,
        DateTimeOffset decidedAt,
        string? comment)
    {
        if (claimId == Guid.Empty) throw new ArgumentException(nameof(claimId));
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException(nameof(currency));

        Id = id;
        CompanyId = companyId;
        ClaimId = claimId;
        DecisionType = decisionType;
        DecisionAmount = decisionAmount;
        Currency = currency;
        DecidedByUserId = decidedByUserId;
        DecidedAt = decidedAt;
        Comment = comment;
    }
}

