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

public sealed class DuplicateBusinessRuleTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public DuplicateBusinessRuleTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Creating_duplicate_contract_returns_400_or_409_and_does_not_create_second_record()
    {
        // Arrange: login as Admin via existing flow
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, password: "Password123!", CancellationToken.None);
        var counterpartyId = await SeedCounterpartyAsync(admin.CompanyId, "Duplicate Test Counterparty");

        using var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client, admin.Email, admin.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var contractName = $"DUPLICATE-CONTRACT-{Guid.NewGuid():N}";

        // 1) First create: should succeed
        client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("n"));
        var firstResponse = await client.PostAsJsonAsync("/api/contracts", new
        {
            name = contractName,
            counterpartyId
        });

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2) Second create with the same "business key": should be rejected
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("n"));
        var secondResponse = await client.PostAsJsonAsync("/api/contracts", new
        {
            name = contractName,
            counterpartyId
        });

        secondResponse.StatusCode.Should().NotBe(HttpStatusCode.Created);
        (secondResponse.StatusCode == HttpStatusCode.BadRequest || secondResponse.StatusCode == HttpStatusCode.Conflict)
            .Should()
            .BeTrue("duplicate create should be rejected with 400 or 409");

        // 3) Verify only one record exists via list endpoint
        var listResponse = await client.GetAsync("/api/contracts");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        payload.ValueKind.Should().Be(JsonValueKind.Object);
        payload.TryGetProperty("items", out var itemsProp).Should().BeTrue();
        itemsProp.ValueKind.Should().Be(JsonValueKind.Array);

        var items = itemsProp.EnumerateArray()
            .Where(x => x.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String && n.GetString() == contractName)
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

