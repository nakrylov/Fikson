using Fixon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fixon.Api.Observability;

/// <summary>
/// Readiness check: DB reachable + migrations applied.
/// Не меняет состояние БД.
/// </summary>
public sealed class EfMigrationsHealthCheck : IHealthCheck
{
    private readonly DbContextOptions<FixonDbContext> _options;

    public EfMigrationsHealthCheck(DbContextOptions<FixonDbContext> options)
    {
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // system context
        await using var db = new FixonDbContext(_options, new Fixon.Infrastructure.Tenancy.FixedTenantProvider(null, true));

        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        if (!canConnect)
        {
            return HealthCheckResult.Unhealthy("Database unreachable");
        }

        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        var pendingList = pending.ToList();
        if (pendingList.Count > 0)
        {
            return HealthCheckResult.Unhealthy("Pending migrations exist", data: new Dictionary<string, object>
            {
                ["pendingMigrations"] = pendingList
            });
        }

        return HealthCheckResult.Healthy("Database ready");
    }
}

