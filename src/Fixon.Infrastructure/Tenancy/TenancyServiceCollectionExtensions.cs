using Microsoft.Extensions.DependencyInjection;

namespace Fixon.Infrastructure.Tenancy;

/// <summary>
/// Extension methods для регистрации tenant infrastructure в DI контейнере.
/// 
/// ВАЖНО: AddHttpContextAccessor должен быть вызван в API проекте перед AddTenancy.
/// </summary>
public static class TenancyServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует tenant infrastructure.
    /// Требует, чтобы AddHttpContextAccessor был вызван ранее.
    /// </summary>
    public static IServiceCollection AddTenancy(this IServiceCollection services)
    {

        // Регистрируем отдельные providers (для композиции)
        services.AddScoped<JwtTenantProvider>();
        services.AddScoped<HeaderTenantProvider>();

        // Composite provider как основной ITenantProvider
        services.AddScoped<ITenantProvider>(sp =>
        {
            var jwtProvider = sp.GetRequiredService<JwtTenantProvider>();
            var headerProvider = sp.GetRequiredService<HeaderTenantProvider>();
            return new CompositeTenantProvider(jwtProvider, headerProvider, allowMissingTenant: false);
        });

        return services;
    }

    /// <summary>
    /// Регистрирует tenant infrastructure с возможностью пропускать tenant для публичных endpoints.
    /// Требует, чтобы AddHttpContextAccessor был вызван ранее.
    /// </summary>
    public static IServiceCollection AddTenancyWithPublicEndpoints(this IServiceCollection services)
    {

        // Регистрируем отдельные providers (для композиции)
        services.AddScoped<JwtTenantProvider>();
        services.AddScoped<HeaderTenantProvider>();

        services.AddScoped<ITenantProvider>(sp =>
        {
            var jwtProvider = sp.GetRequiredService<JwtTenantProvider>();
            var headerProvider = sp.GetRequiredService<HeaderTenantProvider>();
            return new CompositeTenantProvider(jwtProvider, headerProvider, allowMissingTenant: true);
        });

        return services;
    }
}

