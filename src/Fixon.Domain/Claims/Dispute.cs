using Fixon.Domain.Abstractions;

namespace Fixon.Domain.Claims;

/// <summary>
/// Dispute — процесс оспаривания Claim.
/// </summary>
public sealed class Dispute : TenantEntity
{
    public Guid ClaimId { get; private set; }
    public DisputeInitiatedBy InitiatedBy { get; private set; }
    public string Reason { get; private set; } = null!;
    public DisputeStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public Claim? Claim { get; private set; }

    private Dispute() { } // EF

    public Dispute(
        Guid id,
        Guid companyId,
        Guid claimId,
        DisputeInitiatedBy initiatedBy,
        string reason,
        DateTimeOffset openedAt)
    {
        if (claimId == Guid.Empty) throw new ArgumentException(nameof(claimId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException(nameof(reason));

        Id = id;
        CompanyId = companyId;
        ClaimId = claimId;
        InitiatedBy = initiatedBy;
        Reason = reason;
        Status = DisputeStatus.Open;
        OpenedAt = openedAt;
    }

    public void Resolve(DateTimeOffset resolvedAtUtc)
    {
        if (Status != DisputeStatus.Open) throw new InvalidOperationException("Dispute already resolved.");
        Status = DisputeStatus.Resolved;
        ResolvedAt = resolvedAtUtc;
    }
}

