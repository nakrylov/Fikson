using Fixon.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fixon.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core tooling (migrations).
/// Использует SystemTenantProvider для bypass tenant filter при миграциях.
/// </summary>
public sealed class FixonDbContextFactory : IDesignTimeDbContextFactory<FixonDbContext>
{
    public FixonDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("FIXON_CONNECTION_STRING")
            ?? "Host=localhost;Database=fixon;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<FixonDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // System context для миграций (bypass tenant filter)
        var tenantProvider = new SystemTenantProvider();

        return new FixonDbContext(options, tenantProvider);
    }
}


