using Fixon.Domain.Abstractions;
using Fixon.Domain.Users;

namespace Fixon.Domain.Sla;

public sealed class SlaRuleVersion : ActivatableTenantEntity
{
    public Guid SlaRuleId { get; private set; }
    public int VersionNumber { get; private set; }
    public string AppliesWhenJson { get; private set; } = null!;
    public string ConditionJson { get; private set; } = null!;
    public string PenaltyJson { get; private set; } = null!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public SlaRule? SlaRule { get; private set; }
    public User? CreatedByUser { get; private set; }

    public ICollection<SlaEvaluation> SlaEvaluations { get; private set; } = new List<SlaEvaluation>();
    public ICollection<Claims.Claim> Claims { get; private set; } = new List<Claims.Claim>();

    private SlaRuleVersion() { } // EF / serialization

    public SlaRuleVersion(
        Guid id,
        Guid companyId,
        Guid slaRuleId,
        int versionNumber,
        string appliesWhenJson,
        string conditionJson,
        string penaltyJson,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        DateTimeOffset createdAt,
        Guid createdByUserId,
        bool isActive = true)
    {
        Id = id;
        CompanyId = companyId;
        SlaRuleId = slaRuleId;
        VersionNumber = versionNumber;
        AppliesWhenJson = appliesWhenJson;
        ConditionJson = conditionJson;
        PenaltyJson = penaltyJson;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        CreatedAt = createdAt;
        CreatedByUserId = createdByUserId;
        IsActive = isActive;
    }
}


