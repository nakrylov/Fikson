using Fixon.Domain.Abstractions;
using Fixon.Domain.Contracts;

namespace Fixon.Domain.Sla;

/// <summary>
/// Immutable факт нарушения SLA (юридическое основание для Claim).
/// Создаётся на базе SlaEvaluation.
/// </summary>
public sealed class SlaViolation : TenantEntity
{
    public Guid ContractVersionId { get; private set; }
    public Guid SlaRuleVersionId { get; private set; }
    public Guid SlaEvaluationId { get; private set; }

    public string CalculatedValuesJson { get; private set; } = null!;
    public DateTimeOffset DetectedAt { get; private set; }

    public ContractVersion? ContractVersion { get; private set; }
    public SlaRuleVersion? SlaRuleVersion { get; private set; }
    public SlaEvaluation? SlaEvaluation { get; private set; }

    private SlaViolation() { } // EF

    public SlaViolation(
        Guid id,
        Guid companyId,
        Guid contractVersionId,
        Guid slaRuleVersionId,
        Guid slaEvaluationId,
        string calculatedValuesJson,
        DateTimeOffset detectedAt)
    {
        if (string.IsNullOrWhiteSpace(calculatedValuesJson)) throw new ArgumentException(nameof(calculatedValuesJson));

        Id = id;
        CompanyId = companyId;
        ContractVersionId = contractVersionId;
        SlaRuleVersionId = slaRuleVersionId;
        SlaEvaluationId = slaEvaluationId;
        CalculatedValuesJson = calculatedValuesJson;
        DetectedAt = detectedAt;
    }
}

