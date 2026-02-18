using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Fixon.IntegrationTests.Infrastructure;
using Fixon.Domain.Companies;
using Fixon.Domain.Users;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Fixon.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class AuthRoleAuthorizationTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public AuthRoleAuthorizationTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Admin_can_access_protected_endpoint()
    {
        // Arrange: seed admin user in real test DB
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, password: "Password123!", CancellationToken.None);

        using var client = _factory.CreateClient();

        // Act: login as ADMIN via real endpoint
        var login = await LoginAsync(client, admin.Email, admin.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        // Act: call protected endpoint (requires contracts.read)
        var response = await client.GetAsync("/api/contracts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Non_admin_role_gets_403_forbidden_on_protected_endpoint()
    {
        // Arrange: seed non-admin user (ThreePL) in real test DB
        var user = await SeedUserAsync(roleName: Roles.ThreePL, password: "Password123!");

        using var client = _factory.CreateClient();

        // Act: login as non-admin via real endpoint
        var login = await LoginAsync(client, user.Email, user.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        // Act: call the same protected endpoint
        var response = await client.GetAsync("/api/contracts");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        payload.Should().NotBeNull();
        payload!.Token.Should().NotBeNullOrWhiteSpace();
        return payload;
    }

    private async Task<SeededUser> SeedUserAsync(string roleName, string password)
    {
        await TestDbSeeder.EnsureMigratedAsync(_factory.Services, CancellationToken.None);

        using var scope = _factory.Services.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var connectionString = cfg.GetConnectionString("DefaultConnection");
        connectionString.Should().NotBeNullOrWhiteSpace("test DB connection string must be configured");

        var options = new DbContextOptionsBuilder<FixonDbContext>()
            .UseNpgsql(connectionString!)
            .Options;

        // Use system context so tenant query filters won't block seeding / lookup.
        await using var db = new FixonDbContext(options, new SystemTenantProvider());

        var now = DateTimeOffset.UtcNow;
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"user+{roleName.ToLowerInvariant()}+{companyId:n}@integration.test";

        var company = new Company(companyId, $"Integration Test Company {companyId:n}", now, isActive: true);
        db.Companies.Add(company);

        var role = await EnsureRoleAsync(db, roleName, CancellationToken.None);

        var passwordHasher = new PasswordHasher();
        var user = new User(
            id: userId,
            companyId: companyId,
            email: email,
            name: $"Integration Test {roleName}",
            passwordHash: passwordHasher.HashPassword(password),
            createdAt: now,
            isActive: true);

        db.Users.Add(user);
        db.UserRoles.Add(new UserRole(userId: userId, roleId: role.Id));

        // SaaS step 1: create membership so login uses it.
        var membershipRole =
            roleName == Roles.Admin ? MembershipRole.Admin
            : roleName == Roles.ThreePL ? MembershipRole.Viewer
            : MembershipRole.Member;

        db.UserTenantMemberships.Add(new UserTenantMembership(
            id: Guid.NewGuid(),
            userId: userId,
            tenantId: companyId,
            role: membershipRole,
            status: MembershipStatus.Active,
            createdAt: now));

        await db.SaveChangesAsync();

        return new SeededUser(companyId, userId, email, password, roleName);
    }

    private static async Task<Role> EnsureRoleAsync(FixonDbContext db, string roleName, CancellationToken ct)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == roleName, ct);
        if (role is not null) return role;

        db.Roles.Add(new Role(Guid.NewGuid(), roleName));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Another test inserted the same role name concurrently.
            db.ChangeTracker.Clear();
        }

        return await db.Roles.SingleAsync(r => r.Name == roleName, ct);
    }

    private sealed class LoginResponse
    {
        public string Token { get; set; } = default!;
    }

    private sealed record SeededUser(
        Guid CompanyId,
        Guid UserId,
        string Email,
        string Password,
        string RoleName);
}

