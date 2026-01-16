using Fixon.Domain.Audit;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Fixon.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Fixon.Infrastructure.Audit;

/// <summary>
/// Infrastructure audit writer. Application инициирует (вызывает), Infrastructure сохраняет.
/// Audit ≠ Logging: записи append-only.
/// </summary>
public sealed class AuditWriter : IAuditWriter
{
    private readonly DbContextOptions<FixonDbContext> _dbOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditWriter(DbContextOptions<FixonDbContext> dbOptions, IHttpContextAccessor httpContextAccessor)
    {
        _dbOptions = dbOptions;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Guid> WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken)
    {
        // Защита от tenant spoofing: обычный пользователь может писать аудит только в свой tenant.
        // System/admin сценарии будут добавлены позже через отдельный system context.
        EnsureTenantMatchesJwtIfPresent(request.CompanyId);

        // System context (чтобы не зависеть от tenant filter при записи аудита).
        var tenantProvider = new FixedTenantProvider(request.CompanyId, isSystemContext: true);
        await using var db = new FixonDbContext(_dbOptions, tenantProvider);

        var (actorType, actorUserId, actorSystem) = ResolveActor();

        // fail-fast: details must be valid JSON (snapshot/diff/details)
        ValidateJson(request.DetailsJson);

        var id = Guid.NewGuid();

        var audit = new AuditLog(
            id: id,
            companyId: request.CompanyId,
            actorType: actorType,
            actorUserId: actorUserId,
            actorSystem: actorSystem,
            entityType: request.EntityType,
            entityId: request.EntityId,
            action: request.Action,
            correlationId: request.CorrelationId,
            detailsJson: request.DetailsJson,
            previousAuditLogId: request.PreviousAuditLogId,
            timestamp: DateTimeOffset.UtcNow);

        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);

        return id;
    }

    private void EnsureTenantMatchesJwtIfPresent(Guid companyId)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return; // system actor path
        }

        var tenantIdClaim = httpContext.User.FindFirst(FixonClaims.TenantId)?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            throw new InvalidOperationException("Authenticated user is missing valid tenant_id claim.");
        }

        if (tenantId != companyId)
        {
            throw new UnauthorizedAccessException("Cannot write audit record for a different tenant.");
        }
    }

    private (AuditActorType actorType, Guid? actorUserId, string? actorSystem) ResolveActor()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = httpContext.User.FindFirst(FixonClaims.UserId)?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
            {
                return (AuditActorType.User, userId, null);
            }
        }

        // system actor (no PII)
        return (AuditActorType.System, null, Environment.MachineName);
    }

    private static void ValidateJson(string json)
    {
        using var _ = JsonDocument.Parse(json);
    }
}

