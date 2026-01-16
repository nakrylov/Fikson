using Fixon.Application.Bootstrap;
using Microsoft.Extensions.DependencyInjection;

namespace Fixon.Infrastructure.Bootstrap;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBootstrapVerticalSlice(this IServiceCollection services, BootstrapOptions options)
    {
        services.AddSingleton(options);
        services.AddScoped<IBootstrapUseCases, BootstrapUseCases>();
        services.AddScoped<IBootstrapSeeder, BootstrapSeeder>();
        return services;
    }
}

