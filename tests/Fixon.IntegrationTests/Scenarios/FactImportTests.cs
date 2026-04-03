using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Fixon.Domain.Contracts;
using Fixon.Domain.Sla;
using Fixon.Infrastructure.Persistence;
using Fixon.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Fixon.IntegrationTests.Infrastructure;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class FactImportTests : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public FactImportTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Import_facts_csv_returns_imported_count_greater_than_zero()
    {
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, "Password123!", CancellationToken.None);

        using var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client, admin.Email, admin.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        const string csv = """
shipmentId,factType,eventTime,value
SHP-001,temperature,2026-02-01T10:00:00Z,12.5
SHP-002,status,2026-02-01T11:00:00Z,ok
""";

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        content.Add(fileContent, "file", "facts.csv");

        var resp = await client.PostAsync("/api/imports/facts", content);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var payload = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("imported", out var importedProp));
        Assert.True(importedProp.GetInt32() > 0);
        Assert.True(payload.TryGetProperty("claimsGenerated", out var claimsGeneratedProp));
        Assert.True(claimsGeneratedProp.GetInt32() >= 0);
    }

    [Fact]
    public async Task Download_template_without_rules_returns_basic_columns_only()
    {
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, "Password123!", CancellationToken.None);

        using var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client, admin.Email, admin.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/imports/template");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var csv = await resp.Content.ReadAsStringAsync();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(lines);
        Assert.Equal("shipmentId;counterpartyCode;cargoType", lines[0].Trim());
    }

    [Fact]
    public async Task Download_template_with_temperature_rule_includes_temperature_column()
    {
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, "Password123!", CancellationToken.None);
        await SeedActiveContractWithRuleAsync(admin.CompanyId, admin.UserId, "TEMPERATURE");

        using var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client, admin.Email, admin.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/imports/template");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var csv = await resp.Content.ReadAsStringAsync();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(lines);

        var headerColumns = lines[0].Trim().Split(';', StringSplitOptions.TrimEntries);
        Assert.Contains("shipmentId", headerColumns);
        Assert.Contains("counterpartyCode", headerColumns);
        Assert.Contains("cargoType", headerColumns);
        Assert.Contains("temperature", headerColumns);
        Assert.DoesNotContain("documentsMissing", headerColumns);
    }

    private async Task SeedActiveContractWithRuleAsync(Guid tenantId, Guid userId, string metric)
    {
        await TestDbSeeder.EnsureMigratedAsync(_factory.Services, CancellationToken.None);

        using var scope = _factory.Services.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var cs = cfg.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("DefaultConnection is not configured for tests.");
        }

        var options = new DbContextOptionsBuilder<FixonDbContext>()
            .UseNpgsql(cs)
            .Options;

        await using var db = new FixonDbContext(options, new SystemTenantProvider());
        var now = DateTimeOffset.UtcNow;

        var counterpartyId = Guid.NewGuid();
        db.Counterparties.Add(new Counterparty(
            id: counterpartyId,
            companyId: tenantId,
            name: "Template Counterparty",
            externalCode: "TPL-CP",
            isActive: true));

        var contractId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var contract = new Contract(
            id: contractId,
            companyId: tenantId,
            counterpartyId: counterpartyId,
            name: "Template Contract",
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
        db.SlaRules.Add(new SlaRule(
            id: ruleId,
            companyId: tenantId,
            contractId: contractId,
            name: metric,
            createdAt: now,
            isActive: true));
        db.SlaRuleVersions.Add(new SlaRuleVersion(
            id: Guid.NewGuid(),
            companyId: tenantId,
            slaRuleId: ruleId,
            versionNumber: 1,
            appliesWhenJson: "{}",
            conditionJson: JsonSerializer.Serialize(new
            {
                metric,
                @operator = ">",
                threshold = 1
            }),
            penaltyJson: """{"Value":100}""",
            effectiveFrom: now,
            effectiveTo: null,
            createdAt: now,
            createdByUserId: userId,
            isActive: true));

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
