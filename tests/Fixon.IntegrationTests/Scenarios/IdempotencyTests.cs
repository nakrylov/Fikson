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

public sealed class IdempotencyTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public IdempotencyTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_contract_with_same_idempotency_key_returns_same_response_and_does_not_create_duplicate()
    {
        // 1) Bootstrap tenant
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, password: "Password123!", CancellationToken.None);

        // Precondition: Contract requires an existing CounterpartyId (FK).
        var counterpartyId = await SeedCounterpartyAsync(admin.CompanyId, "Idempotency Test Counterparty");

        // 2) Login as Admin
        using var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client, admin.Email, admin.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 3) Single Idempotency-Key for both requests
        var idempotencyKey = Guid.NewGuid().ToString();

        // 4) Valid payload for contract creation
        var payload = new
        {
            name = $"IDEMPOTENCY-CONTRACT-{Guid.NewGuid():N}",
            counterpartyId
        };

        // 5) First POST /api/contracts
        using var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/contracts")
        {
            Content = JsonContent.Create(payload)
        };
        req1.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        var resp1 = await client.SendAsync(req1);
        resp1.StatusCode.Should().Be(HttpStatusCode.Created);

        var body1 = await resp1.Content.ReadAsStringAsync();
        body1.Should().NotBeNullOrWhiteSpace();

        using var doc1 = JsonDocument.Parse(body1);
        var contractId1 = doc1.RootElement.GetProperty("id").GetGuid();

        // 6) Second POST /api/contracts (same payload + same Idempotency-Key)
        using var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/contracts")
        {
            Content = JsonContent.Create(payload)
        };
        req2.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        var resp2 = await client.SendAsync(req2);
        resp2.StatusCode.Should().Be(HttpStatusCode.Created);

        var body2 = await resp2.Content.ReadAsStringAsync();
        body2.Should().NotBeNullOrWhiteSpace();

        using var doc2 = JsonDocument.Parse(body2);
        var contractId2 = doc2.RootElement.GetProperty("id").GetGuid();

        // 7) Assertions
        JsonElement.DeepEquals(doc1.RootElement, doc2.RootElement)
            .Should()
            .BeTrue("idempotent replay must return the same JSON payload (semantic equality)");
        contractId2.Should().Be(contractId1);

        // Verify only one Contract exists (via HTTP)
        var listResp = await client.GetAsync("/api/contracts");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        listPayload.ValueKind.Should().Be(JsonValueKind.Object);
        listPayload.TryGetProperty("items", out var itemsProp).Should().BeTrue();
        itemsProp.ValueKind.Should().Be(JsonValueKind.Array);

        var items = itemsProp.EnumerateArray()
            .Where(x => x.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String && idProp.GetGuid() == contractId1)
            .ToList();

        items.Count.Should().Be(1);
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

        var id = Guid.NewGuid();
        db.Counterparties.Add(new Counterparty(
            id: id,
            companyId: companyId,
            name: name,
            externalCode: null,
            isActive: true));

        await db.SaveChangesAsync();
        return id;
    }

    private sealed class LoginResponse
    {
        public string Token { get; set; } = default!;
    }
}

