using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fixon.IntegrationTests.Infrastructure;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class AuthMeEndpointTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public AuthMeEndpointTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Me_returns_user_and_membership_for_authenticated_user_with_tenant_membership()
    {
        var adminPassword = "Password123!";
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, adminPassword, CancellationToken.None);

        using var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client, admin.Email, admin.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var meResp = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);

        var meJson = await meResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, meJson.ValueKind);

        Assert.True(meJson.TryGetProperty("userId", out var userIdProp));
        Assert.Equal(admin.UserId.ToString(), userIdProp.GetString());

        Assert.True(meJson.TryGetProperty("email", out var emailProp));
        Assert.Equal(admin.Email, emailProp.GetString());

        Assert.True(meJson.TryGetProperty("currentTenantId", out var currentTenantIdProp));
        Assert.Equal(admin.CompanyId.ToString(), currentTenantIdProp.GetString());

        Assert.True(meJson.TryGetProperty("role", out var roleProp));
        Assert.Equal("Admin", roleProp.GetString());

        Assert.True(meJson.TryGetProperty("memberships", out var membershipsProp));
        Assert.Equal(JsonValueKind.Array, membershipsProp.ValueKind);
        Assert.True(membershipsProp.GetArrayLength() >= 1);

        var tenantFound = membershipsProp
            .EnumerateArray()
            .Any(x =>
                x.TryGetProperty("tenantId", out var tenantIdProp)
                && tenantIdProp.GetString() == admin.CompanyId.ToString());

        Assert.True(tenantFound);
    }

    [Fact]
    public async Task Me_returns_null_tenant_and_empty_memberships_for_tenantless_user()
    {
        using var client = _factory.CreateClient();

        var email = $"me+{Guid.NewGuid():N}@integration.test";
        var password = "StrongPass123!";

        var registerResp = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.OK, registerResp.StatusCode);

        var registerJson = await registerResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(registerJson.TryGetProperty("token", out var tokenProp));
        var token = tokenProp.GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var meResp = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);

        var meJson = await meResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, meJson.ValueKind);

        Assert.True(meJson.TryGetProperty("email", out var emailProp));
        Assert.Equal(email, emailProp.GetString());

        Assert.True(meJson.TryGetProperty("currentTenantId", out var tenantIdProp));
        Assert.Equal(JsonValueKind.Null, tenantIdProp.ValueKind);

        Assert.True(meJson.TryGetProperty("memberships", out var membershipsProp));
        Assert.Equal(JsonValueKind.Array, membershipsProp.ValueKind);
        Assert.Equal(0, membershipsProp.GetArrayLength());
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var payload = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("token", out var tokenProp));

        var token = tokenProp.GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }
}
