using Fixon.Domain.Abstractions;
using Fixon.Domain.Contracts;

namespace Fixon.Domain.Sla;

public sealed class SlaRule : ActivatableTenantEntity
{
    public const string ConditionTypeThreshold = "threshold";
    public const string ConditionTypeRange = "range";
    public const string ConditionTypeBoolean = "boolean";

    public Guid ContractId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? ScopeJson { get; private set; }
    public string ConditionType { get; private set; } = ConditionTypeThreshold;
    public decimal? MinValue { get; private set; }
    public decimal? MaxValue { get; private set; }
    public string? EventType { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Contract? Contract { get; private set; }
    public ICollection<SlaRuleVersion> Versions { get; private set; } = new List<SlaRuleVersion>();

    private SlaRule() { } // EF / serialization

    public SlaRule(
        Guid id,
        Guid companyId,
        Guid contractId,
        string name,
        DateTimeOffset createdAt,
        bool isActive = true,
        string? scopeJson = null,
        string conditionType = ConditionTypeThreshold,
        decimal? minValue = null,
        decimal? maxValue = null,
        string? eventType = null,
        string? operatorValue = null,
        decimal? thresholdValue = null)
    {
        if (string.IsNullOrWhiteSpace(conditionType))
        {
            throw new ArgumentException("ConditionType is required.", nameof(conditionType));
        }

        var normalizedConditionType = conditionType.Trim().ToLowerInvariant();
        if (normalizedConditionType == ConditionTypeRange)
        {
            if (!minValue.HasValue || !maxValue.HasValue)
            {
                throw new ArgumentException("MinValue and MaxValue are required for range condition.");
            }

            if (minValue.Value > maxValue.Value)
            {
                throw new ArgumentException("MinValue must be less than or equal to MaxValue.");
            }

            eventType = null;
        }
        else if (normalizedConditionType == ConditionTypeBoolean)
        {
            if (string.IsNullOrWhiteSpace(eventType))
            {
                throw new ArgumentException("EventType is required for boolean condition.", nameof(eventType));
            }

            if (operatorValue is not null || thresholdValue.HasValue || minValue.HasValue || maxValue.HasValue)
            {
                throw new ArgumentException("Boolean condition cannot have operator/threshold/min/max values.");
            }
        }
        else
        {
            normalizedConditionType = ConditionTypeThreshold;
            minValue = null;
            maxValue = null;
            eventType = null;
        }

        Id = id;
        CompanyId = companyId;
        ContractId = contractId;
        Name = name;
        ScopeJson = scopeJson;
        ConditionType = normalizedConditionType;
        MinValue = minValue;
        MaxValue = maxValue;
        EventType = string.IsNullOrWhiteSpace(eventType) ? null : eventType.Trim();
        CreatedAt = createdAt;
        IsActive = isActive;
    }
}


