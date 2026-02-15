using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Fixon.Domain.Companies;
using Fixon.Domain.Contracts;
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

public sealed class ContractsAccessTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public ContractsAccessTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Contracts_access_and_concurrency_behave_correctly_across_roles()
    {
        // All constants are local to the test method, as required.
        var createEndpoint = "/api/contracts";

        // Bootstrap tenant (via existing fixture seed approach) and create users in same tenant.
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, password: "Password123!", CancellationToken.None);
        var pledgor = await SeedUserInTenantAsync(admin.CompanyId, Roles.Pledgor, password: "Password123!");
        var threePl = await SeedUserInTenantAsync(admin.CompanyId, Roles.ThreePL, password: "Password123!");
        var counterpartyId = await SeedCounterpartyAsync(admin.CompanyId, "ContractsAccess Counterparty");

        // Login via HTTP for all users (UserContext comes from token).
        using var clientAdmin = _factory.CreateClient();
        using var clientRead = _factory.CreateClient();
        using var clientNoManage = _factory.CreateClient();

        var adminToken = await LoginAndGetTokenAsync(clientAdmin, admin.Email, admin.Password);
        var readToken = await LoginAndGetTokenAsync(clientRead, pledgor.Email, pledgor.Password);
        var noManageToken = await LoginAndGetTokenAsync(clientNoManage, threePl.Email, threePl.Password);

        clientAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        clientRead.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", readToken);
        clientNoManage.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", noManageToken);

        // Create contract (requires ContractsManage) with Idempotency-Key.
        var idempotencyKey = Guid.NewGuid().ToString();
        var contractName = $"CONTRACT-ACCESS-{Guid.NewGuid():N}";

        using var createReq = new HttpRequestMessage(HttpMethod.Post, createEndpoint)
        {
            Content = JsonContent.Create(new { name = contractName, counterpartyId })
        };
        createReq.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        var createResp = await clientAdmin.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, createBody.ValueKind);
        Assert.True(createBody.TryGetProperty("id", out var idProp));
        var contractId = idProp.GetGuid();
        Assert.NotEqual(Guid.Empty, contractId);

        // Parallel GETs by different roles (Task.WhenAll as required).
        var getUrl = $"/api/contracts/{contractId}";
        var getNoManageTask = clientNoManage.GetAsync(getUrl);
        var getReadTask = clientRead.GetAsync(getUrl);

        await Task.WhenAll(getNoManageTask, getReadTask);

        var getNoManageResp = await getNoManageTask;
        var getReadResp = await getReadTask;

        // User without ContractsRead (and also without ContractsManage) must be forbidden.
        Assert.Equal(HttpStatusCode.Forbidden, getNoManageResp.StatusCode);

        // User with ContractsRead can access.
        Assert.Equal(HttpStatusCode.OK, getReadResp.StatusCode);

        var getReadJson = await getReadResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, getReadJson.ValueKind);
        Assert.True(getReadJson.TryGetProperty("id", out var getIdProp));
        Assert.Equal(contractId, getIdProp.GetGuid());
        Assert.True(getReadJson.TryGetProperty("name", out var getNameProp));
        Assert.Equal(contractName, getNameProp.GetString());
        Assert.True(getReadJson.TryGetProperty("counterpartyId", out var getCpProp));
        Assert.Equal(counterpartyId, getCpProp.GetGuid());

        // Manage user: GET to obtain ETag (xmin) for concurrency.
        var getAdminResp = await clientAdmin.GetAsync(getUrl);
        Assert.Equal(HttpStatusCode.OK, getAdminResp.StatusCode);
        getAdminResp.Headers.ETag.Should().NotBeNull();
        var etag = getAdminResp.Headers.ETag!;
        Assert.False(string.IsNullOrWhiteSpace(etag.Tag));

        // PUT with valid If-Match must succeed (204).
        var nameUpdatedByManage = $"Updated by Manage {Guid.NewGuid():N}";

        using (var putReq1 = new HttpRequestMessage(HttpMethod.Put, getUrl)
        {
            Content = JsonContent.Create(new { name = nameUpdatedByManage })
        })
        {
            putReq1.Headers.IfMatch.Add(new EntityTagHeaderValue(etag.Tag));
            var putResp1 = await clientAdmin.SendAsync(putReq1);
            Assert.Equal(HttpStatusCode.NoContent, putResp1.StatusCode);
        }

        // PUT with stale ETag must fail (412).
        var nameUpdatedByStale = $"Updated by Stale {Guid.NewGuid():N}";
        using (var putReq2 = new HttpRequestMessage(HttpMethod.Put, getUrl)
        {
            Content = JsonContent.Create(new { name = nameUpdatedByStale })
        })
        {
            putReq2.Headers.IfMatch.Add(new EntityTagHeaderValue(etag.Tag));
            var putResp2 = await clientAdmin.SendAsync(putReq2);
            Assert.Equal(HttpStatusCode.PreconditionFailed, putResp2.StatusCode);
        }

        // Final state matches the successful update (and wasn't overwritten by stale update).
        var finalGet = await clientRead.GetAsync(getUrl);
        Assert.Equal(HttpStatusCode.OK, finalGet.StatusCode);

        var finalJson = await finalGet.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(finalJson.TryGetProperty("name", out var finalNameProp));
        Assert.Equal(nameUpdatedByManage, finalNameProp.GetString());
    }

    private async Task<SeededUser> SeedUserInTenantAsync(Guid tenantId, string roleName, string password)
    {
        await TestDbSeeder.EnsureMigratedAsync(_factory.Services, CancellationToken.None);

        using var scope = _factory.Services.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var cs = cfg.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("DefaultConnection is not configured for tests.");

        var options = new DbContextOptionsBuilder<FixonDbContext>()
            .UseNpgsql(cs)
            .Options;

        // System context to bypass filters during seeding.
        await using var db = new FixonDbContext(options, new SystemTenantProvider());

        // Ensure company exists (seed admin already created it, but keep safe).
        var companyExists = await db.Companies.AnyAsync(c => c.Id == tenantId);
        if (!companyExists)
        {
            db.Companies.Add(new Company(tenantId, $"Integration Test Company {tenantId:n}", DateTimeOffset.UtcNow, isActive: true));
        }

        var role = await EnsureRoleAsync(db, roleName);

        var userId = Guid.NewGuid();
        var email = $"user+{roleName.ToLowerInvariant()}+{Guid.NewGuid():N}@integration.test";
        var passwordHasher = new PasswordHasher();

        db.Users.Add(new User(
            id: userId,
            companyId: tenantId,
            email: email,
            name: $"Integration Test {roleName}",
            passwordHash: passwordHasher.HashPassword(password),
            createdAt: DateTimeOffset.UtcNow,
            isActive: true));

        db.UserRoles.Add(new UserRole(userId, role.Id));
        await db.SaveChangesAsync();

        return new SeededUser(email, password);
    }

    private static async Task<Role> EnsureRoleAsync(FixonDbContext db, string roleName)
    {
        var existing = await db.Roles.SingleOrDefaultAsync(r => r.Name == roleName);
        if (existing is not null) return existing;

        db.Roles.Add(new Role(Guid.NewGuid(), roleName));
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
        }

        return await db.Roles.SingleAsync(r => r.Name == roleName);
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var payload = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Token));
        return payload.Token;
    }

    private async Task<Guid> SeedCounterpartyAsync(Guid tenantId, string name)
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

        var id = Guid.NewGuid();
        db.Counterparties.Add(new Counterparty(
            id: id,
            companyId: tenantId,
            name: name,
            externalCode: null,
            isActive: true));

        await db.SaveChangesAsync();
        return id;
    }

    private sealed record SeededUser(string Email, string Password);

    private sealed class LoginResponse
    {
        public string Token { get; set; } = default!;
    }
}

