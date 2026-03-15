using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

public sealed class InviteFlowTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public InviteFlowTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Invite_flow_create_get_accept_enforces_email_binding_and_one_time_use()
    {
        var adminPassword = "Password123!";

        // 1) Admin creates invite
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, adminPassword, CancellationToken.None);

        using var clientAdmin = _factory.CreateClient();
        var adminToken = await LoginAndGetTokenAsync(clientAdmin, admin.Email, admin.Password);
        clientAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var invitedEmail = $"invited+{Guid.NewGuid():N}@integration.test";
        var createInviteResp = await clientAdmin.PostAsJsonAsync(
            $"/api/tenants/{admin.CompanyId}/invites",
            new { email = invitedEmail, role = MembershipRole.Member });

        Assert.Equal(HttpStatusCode.OK, createInviteResp.StatusCode);

        var createInviteJson = await createInviteResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(createInviteJson.TryGetProperty("inviteToken", out var tokenProp));
        var inviteToken = tokenProp.GetString();
        Assert.False(string.IsNullOrWhiteSpace(inviteToken));

        // 2) Non-admin cannot create invite
        var viewer = await SeedUserWithMembershipAsync(admin.CompanyId, MembershipRole.Viewer, password: "Password123!");
        using var clientViewer = _factory.CreateClient();
        var viewerToken = await LoginAndGetTokenAsync(clientViewer, viewer.Email, viewer.Password);
        clientViewer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewerToken);

        var forbiddenResp = await clientViewer.PostAsJsonAsync(
            $"/api/tenants/{admin.CompanyId}/invites",
            new { email = $"x+{Guid.NewGuid():N}@integration.test", role = MembershipRole.Member });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResp.StatusCode);

        // 3) GET invite by token returns metadata
        using var clientAnon = _factory.CreateClient();
        var getInviteResp = await clientAnon.GetAsync($"/api/invites/{inviteToken}");
        Assert.Equal(HttpStatusCode.OK, getInviteResp.StatusCode);

        var getInviteJson = await getInviteResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(getInviteJson.TryGetProperty("email", out var emailProp));
        Assert.Equal(invitedEmail, emailProp.GetString());
        Assert.True(getInviteJson.TryGetProperty("role", out var roleProp));
        Assert.Equal("Member", roleProp.GetString());

        // 4) User registers (tenant-less)
        var registerResp = await clientAnon.PostAsJsonAsync("/api/auth/register", new { email = invitedEmail, password = "StrongPass123!" });
        Assert.Equal(HttpStatusCode.OK, registerResp.StatusCode);
        var registerJson = await registerResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(registerJson.TryGetProperty("token", out var regTokenProp));
        var regToken = regTokenProp.GetString();
        Assert.False(string.IsNullOrWhiteSpace(regToken));

        // 5) User accepts invite
        using var clientUser = _factory.CreateClient();
        clientUser.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regToken);

        var joinResp = await clientUser.PostAsJsonAsync("/api/tenants/join", new { token = inviteToken });
        Assert.Equal(HttpStatusCode.OK, joinResp.StatusCode);
        var joinJson = await joinResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(joinJson.TryGetProperty("token", out var joinedTokenProp));
        var joinedToken = joinedTokenProp.GetString();
        Assert.False(string.IsNullOrWhiteSpace(joinedToken));

        // 6) JWT returned contains tenant_id (we verify via access to tenant-aware endpoint)
        clientUser.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joinedToken);
        var contractsResp = await clientUser.GetAsync("/api/contracts");
        Assert.Equal(HttpStatusCode.OK, contractsResp.StatusCode);

        // 7) Invite reused -> 409
        clientUser.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regToken);
        var reuseResp = await clientUser.PostAsJsonAsync("/api/tenants/join", new { token = inviteToken });
        Assert.Equal(HttpStatusCode.Conflict, reuseResp.StatusCode);

        // 8) Invite email mismatch -> 403
        var mismatchEmail = $"mismatch+{Guid.NewGuid():N}@integration.test";
        var mismatchInviteResp = await clientAdmin.PostAsJsonAsync(
            $"/api/tenants/{admin.CompanyId}/invites",
            new { email = mismatchEmail, role = MembershipRole.Member });
        Assert.Equal(HttpStatusCode.OK, mismatchInviteResp.StatusCode);
        var mismatchInviteJson = await mismatchInviteResp.Content.ReadFromJsonAsync<JsonElement>();
        var mismatchToken = mismatchInviteJson.GetProperty("inviteToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(mismatchToken));

        // Register different user
        var otherEmail = $"other+{Guid.NewGuid():N}@integration.test";
        var otherRegResp = await clientAnon.PostAsJsonAsync("/api/auth/register", new { email = otherEmail, password = "StrongPass123!" });
        Assert.Equal(HttpStatusCode.OK, otherRegResp.StatusCode);
        var otherRegToken = (await otherRegResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

        using var clientOther = _factory.CreateClient();
        clientOther.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherRegToken);
        var mismatchJoinResp = await clientOther.PostAsJsonAsync("/api/tenants/join", new { token = mismatchToken });
        Assert.Equal(HttpStatusCode.Forbidden, mismatchJoinResp.StatusCode);
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var payload = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var token = payload.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    private async Task<(string Email, string Password)> SeedUserWithMembershipAsync(Guid tenantId, MembershipRole role, string password)
    {
        await TestDbSeeder.EnsureMigratedAsync(_factory.Services, CancellationToken.None);

        using var scope = _factory.Services.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var cs = cfg.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("DefaultConnection is not configured for tests.");

        var options = new DbContextOptionsBuilder<FixonDbContext>()
            .UseNpgsql(cs)
            .Options;

        await using var db = new FixonDbContext(options, new SystemTenantProvider());

        // ensure tenant exists
        if (!await db.Companies.AnyAsync(c => c.Id == tenantId))
        {
            db.Companies.Add(new Company(tenantId, $"Integration Test Company {tenantId:n}", DateTimeOffset.UtcNow, isActive: true));
            await db.SaveChangesAsync();
        }

        var userId = Guid.NewGuid();
        var email = $"viewer+{userId:n}@integration.test";
        var now = DateTimeOffset.UtcNow;
        var hasher = new PasswordHasher();

        db.Users.Add(new User(
            id: userId,
            companyId: tenantId,
            email: email,
            name: "Invite Viewer",
            passwordHash: hasher.HashPassword(password),
            createdAt: now,
            isActive: true,
            emailConfirmed: true));

        db.UserTenantMemberships.Add(new UserTenantMembership(
            id: Guid.NewGuid(),
            userId: userId,
            tenantId: tenantId,
            role: role,
            status: MembershipStatus.Active,
            createdAt: now));

        await db.SaveChangesAsync();
        return (email, password);
    }
}

