using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Fixon.IntegrationTests.Infrastructure;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class TenantlessRegistrationTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public TenantlessRegistrationTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_returns_tenantless_jwt_and_tenant_aware_endpoints_forbid_access()
    {
        using var client = _factory.CreateClient();

        var email = $"reg+{Guid.NewGuid():N}@integration.test";
        var password = "StrongPass123!";

        var registerResp = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.OK, registerResp.StatusCode);

        var registerJson = await registerResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, registerJson.ValueKind);
        Assert.True(registerJson.TryGetProperty("token", out var tokenProp));
        var token = tokenProp.GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var payload = DecodeJwtPayload(token!);

        // Tenant-less JWT: must NOT contain tenant_id and role.
        Assert.False(payload.TryGetProperty("tenant_id", out _));
        Assert.False(payload.TryGetProperty("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", out _));

        // Tenant-aware endpoint must be forbidden with tenant-less JWT.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var contractsResp = await client.GetAsync("/api/contracts");
        Assert.Equal(HttpStatusCode.Forbidden, contractsResp.StatusCode);
    }

    private static JsonElement DecodeJwtPayload(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2) throw new InvalidOperationException("Invalid JWT.");

        static byte[] Base64UrlDecode(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }

        var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}

