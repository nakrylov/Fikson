using Fixon.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fixon.Infrastructure.Claims;

/// <summary>
/// ClaimDecision is append-only (юридическая трассируемость).
/// Запрещает Update/Delete для ClaimDecision.
/// </summary>
public sealed class ClaimDecisionImmutabilityInterceptor : SaveChangesInterceptor
{
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

    private static void Enforce(DbContext? db)
    {
        if (db == null) return;

        var entries = db.ChangeTracker.Entries<ClaimDecision>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entries.Count == 0) return;

        throw new InvalidOperationException(
            "ClaimDecision is append-only. Create a new decision record instead of updating/deleting.");
    }
}

