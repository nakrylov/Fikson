using Fixon.Domain.Abstractions;

namespace Fixon.Domain.Facts;

public sealed class FactTypeDefinition : Entity
{
    public const string ValueTypeNumber = "number";
    public const string ValueTypeBoolean = "boolean";
    public const string ValueTypeDatetime = "datetime";
    public const string ValueTypeNone = "none";

    public const string ConditionTypeThreshold = "threshold";
    public const string ConditionTypeRange = "range";
    public const string ConditionTypeBoolean = "boolean";

    public string EventType { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string ValueType { get; private set; } = ValueTypeNone;
    public string DefaultConditionType { get; private set; } = ConditionTypeThreshold;
    public string? Unit { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private FactTypeDefinition() { } // EF

    public FactTypeDefinition(
        Guid id,
        string eventType,
        string displayName,
        string valueType,
        string defaultConditionType,
        string? unit,
        bool isActive,
        DateTimeOffset createdAt)
    {
        Id = id;
        EventType = eventType.Trim();
        DisplayName = displayName.Trim();
        ValueType = valueType.Trim().ToLowerInvariant();
        DefaultConditionType = defaultConditionType.Trim().ToLowerInvariant();
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
        IsActive = isActive;
        CreatedAt = createdAt;
    }
}
