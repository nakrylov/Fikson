using Fixon.Infrastructure.Bootstrap;
using Fixon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fixon.Api.Bootstrap;

/// <summary>
/// Bootstrap-only: applies migrations and seeds single-tenant demo data.
/// </summary>
public sealed class BootstrapHostedService : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<BootstrapHostedService> _logger;

    public BootstrapHostedService(IServiceProvider sp, ILogger<BootstrapHostedService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FixonDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<IBootstrapSeeder>();

        _logger.LogInformation("BOOTSTRAP: applying migrations...");
        await db.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("BOOTSTRAP: seeding demo data...");
        await seeder.EnsureSeededAsync(cancellationToken);

        _logger.LogInformation("BOOTSTRAP: ready.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

