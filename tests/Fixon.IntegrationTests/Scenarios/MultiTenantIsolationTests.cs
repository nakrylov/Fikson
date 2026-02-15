using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Fixon.Domain.Contracts;
using Fixon.Infrastructure.Persistence;
using Fixon.Infrastructure.Tenancy;
using Fixon.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class MultiTenantIsolationTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public MultiTenantIsolationTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Tenant_B_cannot_see_Tenant_A_contracts()
    {
        // --- Bootstrap Tenant A (via existing test seeding approach) ---
        var tenantA = await TestDbSeeder.SeedAdminAsync(_factory.Services, password: "Password123!", CancellationToken.None);
        var counterpartyAId = await SeedCounterpartyAsync(tenantA.CompanyId, "Counterparty A");

        using var clientA = _factory.CreateClient();
        var tokenA = await LoginAndGetTokenAsync(clientA, tenantA.Email, tenantA.Password);
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        // Tenant A creates a contract
        clientA.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("n"));
        var createContractResponse = await clientA.PostAsJsonAsync("/api/contracts", new
        {
            name = "Tenant A Contract",
            counterpartyId = counterpartyAId
        });

        createContractResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdPayload = await createContractResponse.Content.ReadFromJsonAsync<JsonElement>();
        createdPayload.ValueKind.Should().Be(JsonValueKind.Object);
        createdPayload.TryGetProperty("id", out var createdIdProp).Should().BeTrue();
        var contractAId = createdIdProp.GetGuid();
        contractAId.Should().NotBeEmpty();

        // --- Bootstrap Tenant B ---
        var tenantB = await TestDbSeeder.SeedAdminAsync(_factory.Services, password: "Password123!", CancellationToken.None);

        using var clientB = _factory.CreateClient();
        var tokenB = await LoginAndGetTokenAsync(clientB, tenantB.Email, tenantB.Password);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        // Tenant B tries to read contracts list
        var listResponse = await clientB.GetAsync("/api/contracts");

        // Depending on business logic, list may be empty or not found.
        if (listResponse.StatusCode == HttpStatusCode.NotFound)
        {
            return; // acceptable behavior: tenant-scoped list endpoint returns 404
        }

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        listPayload.ValueKind.Should().Be(JsonValueKind.Object);

        listPayload.TryGetProperty("tenantId", out var tenantIdProp).Should().BeTrue();
        tenantIdProp.GetGuid().Should().Be(tenantB.CompanyId);

        listPayload.TryGetProperty("items", out var itemsProp).Should().BeTrue();
        itemsProp.ValueKind.Should().Be(JsonValueKind.Array);

        var items = itemsProp.EnumerateArray().ToList();

        // No data leak from Tenant A
        items.Any(x => x.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String && idProp.GetGuid() == contractAId)
            .Should()
            .BeFalse();

        // Fresh tenant should have no contracts unless created in this test
        items.Count.Should().Be(0);
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        payload.Should().NotBeNull();
        payload!.Token.Should().NotBeNullOrWhiteSpace();
        return payload.Token;
    }

    private async Task<Guid> SeedCounterpartyAsync(Guid companyId, string name)
    {
        await TestDbSeeder.EnsureMigratedAsync(_factory.Services, CancellationToken.None);

        using var scope = _factory.Services.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var connectionString = cfg.GetConnectionString("DefaultConnection");
        connectionString.Should().NotBeNullOrWhiteSpace("test DB connection string must be configured");

        var options = new DbContextOptionsBuilder<FixonDbContext>()
            .UseNpgsql(connectionString!)
            .Options;

        await using var db = new FixonDbContext(options, new SystemTenantProvider());

        var counterpartyId = Guid.NewGuid();
        db.Counterparties.Add(new Counterparty(
            id: counterpartyId,
            companyId: companyId,
            name: name,
            externalCode: null,
            isActive: true));

        await db.SaveChangesAsync();
        return counterpartyId;
    }

    private sealed class LoginResponse
    {
        public string Token { get; set; } = default!;
    }
}

