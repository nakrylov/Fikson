using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
