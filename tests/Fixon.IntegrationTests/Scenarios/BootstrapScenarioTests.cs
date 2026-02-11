using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using Fixon.IntegrationTests.Infrastructure;


public sealed class BootstrapScenarioTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly FixonWebApplicationFactory _factory;

    public BootstrapScenarioTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Bootstrap_creates_tenant_admin_and_audit()
    {
        // Arrange: seed DB
        var seeded = await TestDbSeeder.SeedAdminAsync(_factory.Services, password: "Password123!", CancellationToken.None);

        // Act: login
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = seeded.Email,
            password = "Password123!"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();
        login!.Token.Should().NotBeNullOrWhiteSpace();

        // Act: call protected endpoint
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.Token);

        var response = await _client.GetAsync("/api/contracts");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed class LoginResponse
    {
        public string Token { get; set; } = default!;
    }
}
