using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixon.Api.Endpoints;

public static class ReportsEndpoints
{
    [Authorize(Policy = Permissions.SlaView)]
    public static async Task<IResult> GetSlaCompliance(
        [FromQuery] DateTimeOffset fromUtc,
        [FromQuery] DateTimeOffset toUtc,
        FixonDbContext db,
        CancellationToken ct)
    {
        var items = await db.SlaEvaluations
            .AsNoTracking()
            .Where(x => x.EvaluatedAt >= fromUtc && x.EvaluatedAt <= toUtc)
            .GroupBy(x => new { x.ContractVersionId, x.SlaRuleVersionId })
            .Select(g => new
            {
                g.Key.ContractVersionId,
                g.Key.SlaRuleVersionId,
                pass = g.Count(x => x.EvaluationResult == Fixon.Domain.Sla.SlaEvaluationResult.Pass),
                fail = g.Count(x => x.EvaluationResult == Fixon.Domain.Sla.SlaEvaluationResult.Fail),
                total = g.Count()
            })
            .OrderByDescending(x => x.fail)
            .ToListAsync(ct);

        return Results.Ok(new { fromUtc, toUtc, items });
    }

    // Финансовые отчёты: только роли с claims.read (3PL не имеет)
    [Authorize(Policy = Permissions.ClaimsRead)]
    public static async Task<IResult> GetPenaltiesSummary(
        [FromQuery] DateTimeOffset fromUtc,
        [FromQuery] DateTimeOffset toUtc,
        FixonDbContext db,
        CancellationToken ct)
    {
        var items = await db.Penalties
            .AsNoTracking()
            .Where(x => x.CalculatedAt >= fromUtc && x.CalculatedAt <= toUtc)
            .GroupBy(x => x.Currency)
            .Select(g => new
            {
                currency = g.Key,
                totalAmount = g.Sum(x => x.CalculatedAmount),
                count = g.Count()
            })
            .ToListAsync(ct);

        return Results.Ok(new { fromUtc, toUtc, items });
    }

    [Authorize(Policy = Permissions.AuditRead)]
    public static async Task<IResult> GetAuditFeed(
        [FromQuery] DateTimeOffset fromUtc,
        [FromQuery] DateTimeOffset toUtc,
        [FromQuery] string? entityType,
        [FromQuery] string? correlationId,
        [FromQuery] DateTimeOffset? beforeUtc,
        [FromQuery] int limit,
        FixonDbContext db,
        CancellationToken ct)
    {
        var q = db.AuditLogs
            .AsNoTracking()
            .Where(x => x.Timestamp >= fromUtc && x.Timestamp <= toUtc);

        if (beforeUtc.HasValue)
        {
            q = q.Where(x => x.Timestamp < beforeUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            q = q.Where(x => x.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            q = q.Where(x => x.CorrelationId == correlationId);
        }

        var take = Math.Clamp(limit == 0 ? 200 : limit, 1, 500);

        var items = await q
            .OrderByDescending(x => x.Timestamp)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.EntityType,
                x.EntityId,
                x.Action,
                x.Timestamp,
                x.ActorType,
                x.ActorUserId,
                x.ActorSystem,
                x.CorrelationId,
                x.PreviousAuditLogId,
                x.DetailsJson
            })
            .ToListAsync(ct);

        var nextCursor = items.Count == 0 ? (DateTimeOffset?)null : items.Last().Timestamp;

        return Results.Ok(new { fromUtc, toUtc, beforeUtc, limit = take, nextCursor, items });
    }
}

