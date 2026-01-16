using Fixon.Domain.Abstractions;
using Fixon.Domain.Users;

namespace Fixon.Domain.Contracts;

public sealed class ContractVersion : ActivatableTenantEntity
{
    public Guid ContractId { get; private set; }
    public int VersionNumber { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public string? PdfFilePath { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public ContractVersionStatus Status { get; private set; }
    public DateTimeOffset? SignedAt { get; private set; }

    /// <summary>
    /// Immutable snapshot of SLA rules used for this version (jsonb).
    /// </summary>
    public string SlaRulesSnapshotJson { get; private set; } = "{}";

    /// <summary>
    /// Immutable snapshot of penalty rules used for this version (jsonb).
    /// </summary>
    public string PenaltyRulesSnapshotJson { get; private set; } = "{}";

    public Contract? Contract { get; private set; }
    public User? CreatedByUser { get; private set; }

    public ICollection<Sla.SlaEvaluation> SlaEvaluations { get; private set; } = new List<Sla.SlaEvaluation>();
    public ICollection<Claims.Claim> Claims { get; private set; } = new List<Claims.Claim>();

    private ContractVersion() { } // EF / serialization

    public ContractVersion(
        Guid id,
        Guid companyId,
        Guid contractId,
        int versionNumber,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        string? pdfFilePath,
        DateTimeOffset createdAt,
        Guid createdByUserId,
        bool isActive = true)
    {
        Id = id;
        CompanyId = companyId;
        ContractId = contractId;
        VersionNumber = versionNumber;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        PdfFilePath = pdfFilePath;
        CreatedAt = createdAt;
        CreatedByUserId = createdByUserId;
        IsActive = isActive;
        Status = ContractVersionStatus.Draft;
    }

    public void SetSnapshots(string slaRulesSnapshotJson, string penaltyRulesSnapshotJson)
    {
        if (Status != ContractVersionStatus.Draft)
        {
            throw new InvalidOperationException("Cannot change snapshots after signing.");
        }

        if (string.IsNullOrWhiteSpace(slaRulesSnapshotJson)) throw new ArgumentException(nameof(slaRulesSnapshotJson));
        if (string.IsNullOrWhiteSpace(penaltyRulesSnapshotJson)) throw new ArgumentException(nameof(penaltyRulesSnapshotJson));

        SlaRulesSnapshotJson = slaRulesSnapshotJson;
        PenaltyRulesSnapshotJson = penaltyRulesSnapshotJson;
    }

    public void Sign(DateTimeOffset signedAtUtc)
    {
        if (Status != ContractVersionStatus.Draft) throw new InvalidOperationException("Only Draft version can be signed.");
        Status = ContractVersionStatus.Signed;
        SignedAt = signedAtUtc;
    }

    public void Archive()
    {
        if (Status == ContractVersionStatus.Archived) return;
        Status = ContractVersionStatus.Archived;
    }
}


