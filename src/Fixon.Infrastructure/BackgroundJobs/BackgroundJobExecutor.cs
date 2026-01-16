using Fixon.Infrastructure.Persistence;
using Fixon.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fixon.Infrastructure.BackgroundJobs;

/// <summary>
/// Tenant-aware executor: создаёт отдельный scope, создаёт DbContext с FixedTenantProvider и вызывает handler.
/// Никакого глобального/implicit tenant switching.
/// </summary>
public sealed class BackgroundJobExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public BackgroundJobExecutor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task ExecuteAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var options = sp.GetRequiredService<DbContextOptions<FixonDbContext>>();

        var tenantProvider = new FixedTenantProvider(
            tenantId: job.CompanyId,
            isSystemContext: job.IsSystem);

        await using var dbContext = new FixonDbContext(options, tenantProvider);

        var handler = ResolveHandler(sp, job.JobType);
        var ctx = new BackgroundJobExecutionContext(job, tenantProvider, dbContext, cancellationToken);

        await handler.HandleAsync(ctx);
    }

    private static IBackgroundJobHandler ResolveHandler(IServiceProvider sp, string jobType)
    {
        var handlers = sp.GetServices<IBackgroundJobHandler>();
        var handler = handlers.FirstOrDefault(h => string.Equals(h.JobType, jobType, StringComparison.Ordinal));
        if (handler == null)
        {
            throw new InvalidOperationException($"No job handler registered for type '{jobType}'.");
        }

        return handler;
    }
}

