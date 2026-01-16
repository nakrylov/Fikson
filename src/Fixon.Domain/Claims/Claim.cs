using Fixon.Domain.Abstractions;
using Fixon.Domain.Contracts;
using Fixon.Domain.Financial;
using Fixon.Domain.Sla;
using Fixon.Domain.Users;

namespace Fixon.Domain.Claims;

public sealed class Claim : TenantEntity
{
    public Guid ContractId { get; private set; }
    public Guid ContractVersionId { get; private set; }
    public Guid SlaRuleVersionId { get; private set; }
    public Guid SlaViolationId { get; private set; }
    public ClaimStatus Status { get; private set; }
    /// <summary>
    /// Исходная рассчитанная сумма (фиксируется при создании).
    /// </summary>
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public ContractVersion? ContractVersion { get; private set; }
    public Contract? Contract { get; private set; }
    public SlaViolation? SlaViolation { get; private set; }
    public SlaRuleVersion? SlaRuleVersion { get; private set; }
    public User? CreatedByUser { get; private set; }

    private Claim() { } // EF / serialization

    public Claim(
        Guid id,
        Guid companyId,
        Guid contractId,
        Guid contractVersionId,
        Guid slaRuleVersionId,
        ClaimStatus status,
        decimal amount,
        string currency,
        DateTimeOffset openedAt,
        DateTimeOffset? closedAt,
        Guid slaViolationId,
        Guid createdByUserId)
    {
        Id = id;
        CompanyId = companyId;
        ContractId = contractId;
        ContractVersionId = contractVersionId;
        SlaRuleVersionId = slaRuleVersionId;
        Status = status;
        Amount = amount;
        Currency = currency;
        OpenedAt = openedAt;
        ClosedAt = closedAt;
        SlaViolationId = slaViolationId;
        CreatedByUserId = createdByUserId;
    }

    /// <summary>
    /// Domain-friendly representation of Amount+Currency as Money (без изменения схемы хранения).
    /// </summary>
    public Money GetAmountMoney() => new(Amount, new CurrencyCode(Currency));

    public void Submit()
    {
        if (Status != ClaimStatus.Draft) throw new InvalidOperationException("Only Draft claim can be submitted.");
        Status = ClaimStatus.Submitted;
    }

    public void MarkUnderReview()
    {
        if (Status != ClaimStatus.Submitted) throw new InvalidOperationException("Only Submitted claim can be moved to review.");
        Status = ClaimStatus.UnderReview;
    }

    public void MarkAccepted()
    {
        if (Status != ClaimStatus.UnderReview) throw new InvalidOperationException("Only UnderReview claim can be accepted.");
        Status = ClaimStatus.Accepted;
    }

    public void MarkRejected(DateTimeOffset closedAtUtc)
    {
        if (Status != ClaimStatus.UnderReview) throw new InvalidOperationException("Only UnderReview claim can be rejected.");
        Status = ClaimStatus.Rejected;
        ClosedAt = closedAtUtc;
    }

    public void MarkDisputed()
    {
        if (Status != ClaimStatus.Accepted) throw new InvalidOperationException("Only Accepted claim can be disputed.");
        Status = ClaimStatus.Disputed;
    }

    public void MarkResolved(DateTimeOffset closedAtUtc)
    {
        if (Status != ClaimStatus.Disputed) throw new InvalidOperationException("Only Disputed claim can be resolved.");
        Status = ClaimStatus.Resolved;
        ClosedAt = closedAtUtc;
    }

    public void Cancel(DateTimeOffset closedAtUtc)
    {
        if (Status is ClaimStatus.Rejected or ClaimStatus.Resolved or ClaimStatus.Cancelled)
        {
            throw new InvalidOperationException("Claim is already closed.");
        }

        if (Status is ClaimStatus.Accepted or ClaimStatus.Disputed)
        {
            throw new InvalidOperationException("Cannot cancel claim after decision.");
        }

        Status = ClaimStatus.Cancelled;
        ClosedAt = closedAtUtc;
    }
}


