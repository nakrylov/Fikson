using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using Fixon.IntegrationTests.Infrastructure;

namespace Fixon.IntegrationTests.Scenarios;

public class AuthBootstrapTests : IClassFixture<FixonWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly FixonWebApplicationFactory _factory;

    public AuthBootstrapTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Bootstrap_and_login_should_work()
    {
        // Arrange: seed dedicated test DB
        var seeded = await TestDbSeeder.SeedAdminAsync(_factory.Services, password: "Password123!", CancellationToken.None);

        // Act: real login over HTTP
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = seeded.Email,
            password = "Password123!"
        });

        loginResponse.EnsureSuccessStatusCode();

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        loginResult.Should().NotBeNull();
        loginResult!.Token.Should().NotBeNullOrWhiteSpace();

        // 3️⃣ Используем токен
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult.Token);

        var contractsResponse = await _client.GetAsync("/api/contracts");

        contractsResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    private class LoginResponse
    {
        public string Token { get; set; } = default!;
    }
}
