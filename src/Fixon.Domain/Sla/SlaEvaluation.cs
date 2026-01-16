using Fixon.Domain.Abstractions;
using Fixon.Domain.Contracts;

namespace Fixon.Domain.Sla;

public sealed class SlaEvaluation : TenantEntity
{
    public Guid ContractVersionId { get; private set; }
    public Guid SlaRuleVersionId { get; private set; }
    public SlaEvaluationResult EvaluationResult { get; private set; }
    public string CalculatedValuesJson { get; private set; } = null!;
    public DateTimeOffset EvaluatedAt { get; private set; }

    public ContractVersion? ContractVersion { get; private set; }
    public SlaRuleVersion? SlaRuleVersion { get; private set; }

    private SlaEvaluation() { } // EF / serialization

    public SlaEvaluation(
        Guid id,
        Guid companyId,
        Guid contractVersionId,
        Guid slaRuleVersionId,
        SlaEvaluationResult evaluationResult,
        string calculatedValuesJson,
        DateTimeOffset evaluatedAt)
    {
        Id = id;
        CompanyId = companyId;
        ContractVersionId = contractVersionId;
        SlaRuleVersionId = slaRuleVersionId;
        EvaluationResult = evaluationResult;
        CalculatedValuesJson = calculatedValuesJson;
        EvaluatedAt = evaluatedAt;
    }
}


