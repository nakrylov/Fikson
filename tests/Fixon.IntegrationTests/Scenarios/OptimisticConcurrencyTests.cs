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

public sealed class OptimisticConcurrencyTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public OptimisticConcurrencyTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Lost_update_is_prevented_by_xmin_etag_and_if_match()
    {
        // Bootstrap tenant + login as Admin (existing flow)
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, password: "Password123!", CancellationToken.None);
        var counterpartyId = await SeedCounterpartyAsync(admin.CompanyId, "Concurrency Test Counterparty");

        using var bootstrapClient = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(bootstrapClient, admin.Email, admin.Password);

        // Create contract
        bootstrapClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        bootstrapClient.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("n"));

        var initialName = $"CONC-ORIGINAL-{Guid.NewGuid():N}";
        var createResponse = await bootstrapClient.PostAsJsonAsync("/api/contracts", new
        {
            name = initialName,
            counterpartyId
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        created.ValueKind.Should().Be(JsonValueKind.Object);
        created.TryGetProperty("id", out var idProp).Should().BeTrue();
        var contractId = idProp.GetGuid();
        contractId.Should().NotBeEmpty();

        // Simulate two clients A and B.
        using var clientA = _factory.CreateClient();
        using var clientB = _factory.CreateClient();

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Both clients read the contract and capture the same ETag (xmin)
        var getA = clientA.GetAsync($"/api/contracts/{contractId}");
        var getB = clientB.GetAsync($"/api/contracts/{contractId}");

        await Task.WhenAll(getA, getB);

        var getRespA = await getA;
        var getRespB = await getB;

        getRespA.StatusCode.Should().Be(HttpStatusCode.OK);
        getRespB.StatusCode.Should().Be(HttpStatusCode.OK);

        getRespA.Headers.ETag.Should().NotBeNull();
        getRespB.Headers.ETag.Should().NotBeNull();

        var etagA = getRespA.Headers.ETag!;
        var etagB = getRespB.Headers.ETag!;

        etagA.Tag.Should().NotBeNullOrWhiteSpace();
        etagB.Tag.Should().NotBeNullOrWhiteSpace();
        etagB.Tag.Should().Be(etagA.Tag);

        var nameA = $"Updated by A {Guid.NewGuid():N}";
        var nameB = $"Updated by B {Guid.NewGuid():N}";

        // Client A: PUT with If-Match (fresh ETag) -> 204 NoContent
        using (var reqA = new HttpRequestMessage(HttpMethod.Put, $"/api/contracts/{contractId}")
        {
            Content = JsonContent.Create(new { name = nameA })
        })
        {
            reqA.Headers.IfMatch.Add(new EntityTagHeaderValue(etagA.Tag));
            var respA = await clientA.SendAsync(reqA);
            respA.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // Client B: PUT with the same (now stale) ETag -> 412 PreconditionFailed
        using (var reqB = new HttpRequestMessage(HttpMethod.Put, $"/api/contracts/{contractId}")
        {
            Content = JsonContent.Create(new { name = nameB })
        })
        {
            reqB.Headers.IfMatch.Add(new EntityTagHeaderValue(etagB.Tag));
            var respB = await clientB.SendAsync(reqB);
            respB.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        }

        // Final state must match Client A update (and must NOT be overwritten by Client B)
        var finalGet = await clientA.GetAsync($"/api/contracts/{contractId}");
        finalGet.StatusCode.Should().Be(HttpStatusCode.OK);

        var finalPayload = await finalGet.Content.ReadFromJsonAsync<JsonElement>();
        finalPayload.ValueKind.Should().Be(JsonValueKind.Object);
        finalPayload.TryGetProperty("name", out var nameProp).Should().BeTrue();
        nameProp.GetString().Should().Be(nameA);
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

