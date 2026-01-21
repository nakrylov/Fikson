using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Fixon.Infrastructure.Persistence;

public sealed class FixonWebApplicationFactory
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Удаляем существующий DbContext
            var descriptor = services.Single(
                d => d.ServiceType == typeof(DbContextOptions<FixonDbContext>)
            );

            services.Remove(descriptor);

            // ⚠️ ВРЕМЕННО: локальный PostgreSQL
            services.AddDbContext<FixonDbContext>(options =>
                options.UseNpgsql(
                    "Host=pg4.sweb.ru;Port=5433;Database=n3kinboxru_fxn;Username=n3kinboxru_fxn;Password=VYC7%FeQZKME3XT3"
                )
            );

            // Применяем миграции
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FixonDbContext>();
            db.Database.Migrate();
        });
    }
}
