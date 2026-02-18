using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Fixon.Domain.Companies;
using Fixon.Domain.Users;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Fixon.Infrastructure.Tenancy;
using Fixon.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class TenantSwitchTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public TenantSwitchTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_jwt_contains_tenant_id_and_role_and_switch_tenant_returns_new_jwt()
    {
        // Arrange: create a user with memberships in two tenants.
        var password = "Password123!";
        await TestDbSeeder.EnsureMigratedAsync(_factory.Services, CancellationToken.None);

        Guid tenantA;
        Guid tenantB;
        string email;

        using (var scope = _factory.Services.CreateScope())
        {
            var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var cs = cfg.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("DefaultConnection is not configured for tests.");

            var options = new DbContextOptionsBuilder<FixonDbContext>()
                .UseNpgsql(cs)
                .Options;

            await using var db = new FixonDbContext(options, new SystemTenantProvider());

            var now = DateTimeOffset.UtcNow;
            tenantA = Guid.NewGuid();
            tenantB = Guid.NewGuid();

            db.Companies.Add(new Company(tenantA, $"Tenant A {tenantA:n}", now, isActive: true));
            db.Companies.Add(new Company(tenantB, $"Tenant B {tenantB:n}", now, isActive: true));

            // Ensure legacy role exists for completeness (not used by login anymore).
            var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == Roles.Admin);
            if (role is null)
            {
                role = new Role(Guid.NewGuid(), Roles.Admin);
                db.Roles.Add(role);
                await db.SaveChangesAsync();
            }

            var userId = Guid.NewGuid();
            email = $"switch+{userId:n}@integration.test";

            var hasher = new PasswordHasher();
            db.Users.Add(new User(
                id: userId,
                companyId: tenantA,
                email: email,
                name: "Switch Tenant User",
                passwordHash: hasher.HashPassword(password),
                createdAt: now,
                isActive: true));

            db.UserRoles.Add(new UserRole(userId, role.Id));

            // Membership in tenant A: Viewer
            db.UserTenantMemberships.Add(new UserTenantMembership(
                id: Guid.NewGuid(),
                userId: userId,
                tenantId: tenantA,
                role: MembershipRole.Viewer,
                status: MembershipStatus.Active,
                createdAt: now));

            // Membership in tenant B: Admin
            db.UserTenantMemberships.Add(new UserTenantMembership(
                id: Guid.NewGuid(),
                userId: userId,
                tenantId: tenantB,
                role: MembershipRole.Admin,
                status: MembershipStatus.Active,
                createdAt: now.AddSeconds(1)));

            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();

        // Act 1: login (should pick first active membership by CreatedAt => tenantA, Viewer).
        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        var loginJson = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, loginJson.ValueKind);
        Assert.True(loginJson.TryGetProperty("token", out var tokenProp));
        var token = tokenProp.GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        // Decode JWT payload (no signature verification needed for claim presence checks).
        var payload1 = DecodeJwtPayload(token!);
        Assert.Equal(tenantA.ToString(), GetString(payload1, "tenant_id"));
        // Role claim uses ClaimTypes.Role URI
        Assert.Equal("Viewer", GetString(payload1, "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Viewer should NOT have contracts.read -> /api/contracts forbidden.
        var contractsAsViewer = await client.GetAsync("/api/contracts");
        Assert.Equal(HttpStatusCode.Forbidden, contractsAsViewer.StatusCode);

        // Act 2: switch to tenantB (Admin) and get a new JWT.
        var switchResp = await client.PostAsJsonAsync("/api/auth/switch-tenant", new { tenantId = tenantB });
        Assert.Equal(HttpStatusCode.OK, switchResp.StatusCode);

        var switchJson = await switchResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(switchJson.TryGetProperty("token", out var token2Prop));
        var token2 = token2Prop.GetString();
        Assert.False(string.IsNullOrWhiteSpace(token2));

        var payload2 = DecodeJwtPayload(token2!);
        Assert.Equal(tenantB.ToString(), GetString(payload2, "tenant_id"));
        Assert.Equal("Admin", GetString(payload2, "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"));

        // New token should allow access.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);
        var contractsAsAdmin = await client.GetAsync("/api/contracts");
        Assert.Equal(HttpStatusCode.OK, contractsAsAdmin.StatusCode);
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

    private static string? GetString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var p)) return null;
        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
    }
}

