using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fixon.Infrastructure.Persistence;

namespace Fixon.IntegrationTests.Infrastructure;

public sealed class FixonWebApplicationFactory
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FixonDbContext>)
            );

            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<FixonDbContext>(options =>
            {
                options.UseInMemoryDatabase("FixonTests");
            });
        });
    }
}
