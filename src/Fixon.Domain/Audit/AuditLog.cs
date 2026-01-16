using Fixon.Domain.Abstractions;
using Fixon.Domain.Companies;
using Fixon.Domain.Users;

namespace Fixon.Domain.Audit;

public sealed class AuditLog : TenantEntity
{
    public AuditActorType ActorType { get; private set; }

    /// <summary>
    /// User actor (nullable for system actions).
    /// </summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>
    /// System actor identifier (optional, e.g., background-job worker id).
    /// </summary>
    public string? ActorSystem { get; private set; }

    public string EntityType { get; private set; } = null!;
    public string EntityId { get; private set; } = null!;
    public string Action { get; private set; } = null!;

    public string? CorrelationId { get; private set; }

    /// <summary>
    /// Snapshot/Diff/Details для воспроизводимости (jsonb).
    /// </summary>
    public string DetailsJson { get; private set; } = null!;

    /// <summary>
    /// Link to previous audit record (for chained adjustments / corrections).
    /// </summary>
    public Guid? PreviousAuditLogId { get; private set; }

    public DateTimeOffset Timestamp { get; private set; }

    public Company? Company { get; private set; }
    public User? ActorUser { get; private set; }
    public AuditLog? Previous { get; private set; }

    private AuditLog() { } // EF / serialization

    public AuditLog(
        Guid id,
        Guid companyId,
        AuditActorType actorType,
        Guid? actorUserId,
        string? actorSystem,
        string entityType,
        string entityId,
        string action,
        string? correlationId,
        string detailsJson,
        Guid? previousAuditLogId,
        DateTimeOffset timestamp)
    {
        Id = id;
        CompanyId = companyId;
        ActorType = actorType;
        ActorUserId = actorUserId;
        ActorSystem = actorSystem;
        EntityType = entityType;
        EntityId = entityId;
        Action = action;
        CorrelationId = correlationId;
        DetailsJson = detailsJson;
        PreviousAuditLogId = previousAuditLogId;
        Timestamp = timestamp;
    }
}


