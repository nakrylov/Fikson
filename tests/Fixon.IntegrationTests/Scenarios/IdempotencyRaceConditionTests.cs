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

public sealed class IdempotencyRaceConditionTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public IdempotencyRaceConditionTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Parallel_posts_with_same_idempotency_key_do_not_create_duplicates()
    {
        // 1) Bootstrap tenant
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, password: "Password123!", CancellationToken.None);

        // Precondition: Contract requires an existing CounterpartyId (FK).
        var counterpartyId = await SeedCounterpartyAsync(admin.CompanyId, "Idempotency Race Counterparty");

        // 2) Login as Admin
        using var authClient = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(authClient, admin.Email, admin.Password);

        // 3) Valid payload
        var payload = new
        {
            name = $"IDEMPOTENCY-RACE-{Guid.NewGuid():N}",
            counterpartyId
        };

        // 4) Single idempotency key
        var idempotencyKey = Guid.NewGuid().ToString();

        // 5) Two clients
        using var client1 = _factory.CreateClient();
        using var client2 = _factory.CreateClient();
        client1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 6) Parallel POSTs with same headers/body
        var task1 = SendCreateContractAsync(client1, payload, idempotencyKey);
        var task2 = SendCreateContractAsync(client2, payload, idempotencyKey);

        var (resp1, body1) = await task1;
        var (resp2, body2) = await task2;

        // 7) Assertions
        resp1.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        resp2.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);

        // At least one must be 201
        (resp1.StatusCode == HttpStatusCode.Created || resp2.StatusCode == HttpStatusCode.Created)
            .Should()
            .BeTrue();

        // Second may be 201 (replay) or 200 (alternative implementations), but never 409/500.
        var allowed = new[] { HttpStatusCode.Created, HttpStatusCode.OK };
        allowed.Should().Contain(resp1.StatusCode);
        allowed.Should().Contain(resp2.StatusCode);

        // If both returned JSON bodies with id, they must match.
        var id1 = TryGetContractId(body1);
        var id2 = TryGetContractId(body2);
        if (id1.HasValue && id2.HasValue)
        {
            id2.Value.Should().Be(id1.Value);
        }

        // Verify only one Contract exists (via HTTP)
        using var verifyClient = _factory.CreateClient();
        verifyClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var listResp = await verifyClient.GetAsync("/api/contracts");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        listPayload.ValueKind.Should().Be(JsonValueKind.Object);
        listPayload.TryGetProperty("items", out var itemsProp).Should().BeTrue();
        itemsProp.ValueKind.Should().Be(JsonValueKind.Array);
        itemsProp.GetArrayLength().Should().Be(1);
    }

    private static async Task<(HttpResponseMessage response, string body)> SendCreateContractAsync(
        HttpClient client,
        object payload,
        string idempotencyKey)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/contracts")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        var resp = await client.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        return (resp, body);
    }

    private static Guid? TryGetContractId(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("id", out var idProp)) return null;
            if (idProp.ValueKind != JsonValueKind.String) return null;
            return idProp.GetGuid();
        }
        catch
        {
            return null;
        }
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

