using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Fixon.IntegrationTests.Infrastructure;

public sealed class FixonWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Добавляем тестовые переменные конфигурации
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET_KEY"] = "TEST_SECRET_KEY_SHOULD_BE_LONG_ENOUGH_123456",
                ["JWT_ISSUER"] = "fixon-tests",
                ["JWT_AUDIENCE"] = "fixon-tests"
            });
        });
    }
}
