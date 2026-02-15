using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Fixon.IntegrationTests.Infrastructure;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class AuthIntegrationTests : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthIntegrationTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_returns_jwt_and_allows_calling_protected_endpoint()
    {
        // Arrange: create a real user in the test database (so we can use the real /api/auth/login)
        var seeded = await TestDbSeeder.SeedAdminAsync(
            _factory.Services,
            password: "Password123!",
            CancellationToken.None);

        // 1) Login with seeded credentials
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = seeded.Email,
            password = seeded.Password
        });

        // 2) Assert login OK + JWT issued
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();
        login!.Token.Should().NotBeNullOrWhiteSpace();
        login.TenantId.Should().Be(seeded.CompanyId);

        // 3) Call protected endpoint with Bearer token
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        var contractsResponse = await _client.GetAsync("/api/contracts");

        // 4) Assert not unauthorized and response shape
        contractsResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        contractsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await contractsResponse.Content.ReadFromJsonAsync<JsonElement>();
        payload.ValueKind.Should().Be(JsonValueKind.Object);

        payload.TryGetProperty("tenantId", out var tenantIdProp).Should().BeTrue();
        tenantIdProp.GetGuid().Should().Be(seeded.CompanyId);

        payload.TryGetProperty("items", out var itemsProp).Should().BeTrue();
        itemsProp.ValueKind.Should().Be(JsonValueKind.Array);
    }

    private sealed class LoginResponse
    {
        public string Token { get; set; } = default!;
        public Guid TenantId { get; set; }
    }
}

