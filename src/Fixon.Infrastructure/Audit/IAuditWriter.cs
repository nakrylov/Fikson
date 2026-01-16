using Fixon.Domain.Audit;

namespace Fixon.Infrastructure.Audit;

public sealed record AuditWriteRequest(
    Guid CompanyId,
    string EntityType,
    string EntityId,
    string Action,
    string DetailsJson,
    string? CorrelationId = null,
    Guid? PreviousAuditLogId = null);

public interface IAuditWriter
{
    Task<Guid> WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken);
}

