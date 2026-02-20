using Fixon.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fixon.Infrastructure.Tenancy;

/// <summary>
/// Write-side hard guard against cross-tenant writes.
/// Designed to catch developer mistakes (e.g., setting CompanyId from input) and prevent data leakage.
/// </summary>
public sealed class TenantWriteGuardInterceptor : SaveChangesInterceptor
{
    private readonly ITenantProvider _tenantProvider;

    public TenantWriteGuardInterceptor(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Enforce(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Enforce(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Enforce(DbContext? db)
    {
        if (db == null) return;
        if (_tenantProvider.IsSystemContext) return;

        // Validate any added/modified tenant-scoped entities
        var tenantEntries = db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .Where(e => e.Entity is TenantEntity)
            .ToList();

        // Model B: some endpoints allow tenant-less identity operations.
        // If there are NO tenant-scoped entities being written, we don't require TenantId.
        if (tenantEntries.Count == 0)
        {
            return;
        }

        var tenantId = _tenantProvider.TenantId;
        if (!tenantId.HasValue)
        {
            throw new InvalidOperationException("TenantId is not resolved for this request.");
        }

        foreach (var entry in tenantEntries)
        {
            var entity = (TenantEntity)entry.Entity;
            if (entity.CompanyId == Guid.Empty)
            {
                throw new InvalidOperationException($"Tenant-scoped entity {entity.GetType().Name} has empty CompanyId.");
            }

            if (entity.CompanyId != tenantId.Value)
            {
                throw new UnauthorizedAccessException(
                    $"Cross-tenant write blocked for {entity.GetType().Name}. " +
                    $"Current tenant={tenantId.Value}, entity.CompanyId={entity.CompanyId}.");
            }
        }
    }
}

