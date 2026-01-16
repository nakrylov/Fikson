using Fixon.Infrastructure.Persistence;
using Fixon.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fixon.Infrastructure.Auth;

/// <summary>
/// Extension methods для регистрации DbContext с tenant context.
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Регистрирует FixonDbContext с tenant provider из DI.
    /// </summary>
    public static IServiceCollection AddFixonDbContext(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<FixonDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);

            // Получаем tenant provider из DI для применения query filters
            var tenantProvider = sp.GetRequiredService<ITenantProvider>();
            // DbContext создаётся через factory, который получает ITenantProvider
        });

        // Регистрируем factory для создания DbContext с tenant context
        services.AddScoped<FixonDbContext>(sp =>
        {
            var options = sp.GetRequiredService<DbContextOptions<FixonDbContext>>();
            var tenantProvider = sp.GetRequiredService<ITenantProvider>();
            return new FixonDbContext(options, tenantProvider);
        });

        return services;
    }
}

