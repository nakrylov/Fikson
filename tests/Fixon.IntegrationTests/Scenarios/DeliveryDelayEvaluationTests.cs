using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fixon.Domain.Contracts;
using Fixon.Domain.Facts;
using Fixon.Domain.Sla;
using Fixon.Infrastructure.Persistence;
using Fixon.Infrastructure.Tenancy;
using Fixon.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class DeliveryDelayEvaluationTests : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public DeliveryDelayEvaluationTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Run_evaluation_generates_claims_from_delivery_delay_facts()
    {
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, "Password123!", CancellationToken.None);
        await SeedContractRuleAndFactAsync(admin.CompanyId, admin.UserId);

        using var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client, admin.Email, admin.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var runResp = await client.PostAsync("/api/evaluation/run", content: null);
        Assert.Equal(HttpStatusCode.OK, runResp.StatusCode);

        var runPayload = await runResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(runPayload.TryGetProperty("claimsGenerated", out var generatedProp));
        Assert.True(generatedProp.GetInt32() > 0);

        // Re-run should not generate duplicates for the same shipment/rule pair.
        var runAgainResp = await client.PostAsync("/api/evaluation/run", content: null);
        Assert.Equal(HttpStatusCode.OK, runAgainResp.StatusCode);
        var runAgainPayload = await runAgainResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, runAgainPayload.GetProperty("claimsGenerated").GetInt32());
    }

    private async Task SeedContractRuleAndFactAsync(Guid tenantId, Guid userId)
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
        var now = DateTimeOffset.UtcNow;

        var counterpartyId = Guid.NewGuid();
        db.Counterparties.Add(new Counterparty(
            id: counterpartyId,
            companyId: tenantId,
            name: "Evaluation Counterparty",
            externalCode: null,
            isActive: true));

        var contractId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var contract = new Contract(
            id: contractId,
            companyId: tenantId,
            counterpartyId: counterpartyId,
            name: "Delivery Delay Contract",
            createdAt: now,
            isActive: true);

        var contractVersion = new ContractVersion(
            id: versionId,
            companyId: tenantId,
            contractId: contractId,
            versionNumber: 1,
            effectiveFrom: now,
            effectiveTo: null,
            pdfFilePath: null,
            createdAt: now,
            createdByUserId: userId,
            isActive: true);
        contractVersion.SetSnapshots("{}", "{}");
        contractVersion.Sign(now);

        contract.ActivateVersion(versionId);

        db.Contracts.Add(contract);
        db.ContractVersions.Add(contractVersion);

        var ruleId = Guid.NewGuid();
        var ruleVersionId = Guid.NewGuid();
        db.SlaRules.Add(new SlaRule(
            id: ruleId,
            companyId: tenantId,
            contractId: contractId,
            name: "DELIVERY_DELAY",
            createdAt: now,
            isActive: true));

        db.SlaRuleVersions.Add(new SlaRuleVersion(
            id: ruleVersionId,
            companyId: tenantId,
            slaRuleId: ruleId,
            versionNumber: 1,
            appliesWhenJson: "{}",
            conditionJson: """{"factType":"DELIVERY_DELAY","operator":">","threshold":30}""",
            penaltyJson: """{"Value":500}""",
            effectiveFrom: now,
            effectiveTo: null,
            createdAt: now,
            createdByUserId: userId,
            isActive: true));

        db.Facts.Add(new Fact(
            id: Guid.NewGuid(),
            companyId: tenantId,
            contractId: contractId,
            externalReference: "SHIP-001",
            factType: "DELIVERY_DELAY",
            attributesJson: """{"shipmentId":"SHIP-001","valueNumber":45}""",
            occurredAt: now,
            createdAt: now));

        await db.SaveChangesAsync();
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var payload = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("token", out var tokenProp));

        var token = tokenProp.GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }
}
