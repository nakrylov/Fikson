using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Fixon.IntegrationTests.Infrastructure;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class RegisterAndCreateTenantFlowTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public RegisterAndCreateTenantFlowTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_then_create_tenant_returns_tenant_jwt_with_admin_role()
    {
        using var client = _factory.CreateClient();

        var email = $"flow+{Guid.NewGuid():N}@integration.test";
        var password = "StrongPass123!";

        // Step 1: register -> tenant-less token
        var registerResp = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.OK, registerResp.StatusCode);

        var registerJson = await registerResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(registerJson.TryGetProperty("token", out var tokenProp));
        var token1 = tokenProp.GetString();
        Assert.False(string.IsNullOrWhiteSpace(token1));

        var payload1 = DecodeJwtPayload(token1!);
        Assert.False(payload1.TryGetProperty("tenant_id", out _));

        // Step 2: create tenant using tenant-less token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);
        var createTenantResp = await client.PostAsJsonAsync("/api/tenants", new { name = $"Tenant {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.OK, createTenantResp.StatusCode);

        var tenantJson = await createTenantResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(tenantJson.TryGetProperty("token", out var token2Prop));
        var token2 = token2Prop.GetString();
        Assert.False(string.IsNullOrWhiteSpace(token2));

        var payload2 = DecodeJwtPayload(token2!);

        Assert.True(payload2.TryGetProperty("tenant_id", out var tenantIdProp));
        Assert.Equal(JsonValueKind.String, tenantIdProp.ValueKind);

        Assert.True(payload2.TryGetProperty("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", out var roleProp));
        Assert.Equal("Admin", roleProp.GetString());

        // Step 3: with tenant token, tenant-aware endpoint works.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);
        var contractsResp = await client.GetAsync("/api/contracts");
        Assert.Equal(HttpStatusCode.OK, contractsResp.StatusCode);
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

