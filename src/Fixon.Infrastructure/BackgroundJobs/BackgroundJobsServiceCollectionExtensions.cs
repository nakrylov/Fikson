using Fixon.Infrastructure.BackgroundJobs.Hosted;
using Fixon.Infrastructure.BackgroundJobs.SlaRecalculation;
using Fixon.Infrastructure.Audit;
using Microsoft.Extensions.DependencyInjection;

namespace Fixon.Infrastructure.BackgroundJobs;

public static class BackgroundJobsServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        Action<BackgroundJobRunnerOptions>? configure = null)
    {
        var options = new BackgroundJobRunnerOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IBackgroundJobQueue, BackgroundJobQueue>();
        services.AddSingleton<BackgroundJobExecutor>();
        // Runner uses scoped services (DbContextOptions, AuditWriter), so it must be scoped.
        services.AddScoped<BackgroundJobRunner>();

        // Example job (delegates to use case)
        services.AddScoped<ISlaRecalculationUseCase, NoopSlaRecalculationUseCase>();
        services.AddScoped<IBackgroundJobHandler, SlaRecalculationJobHandler>();

        // Hosted service is registered in API (to avoid running in migrations/tools contexts)
        services.AddHostedService<BackgroundJobHostedService>();

        return services;
    }
}

