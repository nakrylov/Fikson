using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fixon.IntegrationTests.Infrastructure;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class HighContentionIdempotencyTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public HighContentionIdempotencyTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_contract_with_single_idempotency_key_survives_high_contention()
    {
        // All values are local to the test method (as required).
        var password = "Password123!";
        var endpoint = "/api/contracts";
        var parallelism = 50;
        var maxDuration = TimeSpan.FromSeconds(10);

        // Bootstrap tenant + admin, seed required counterparty (FK precondition).
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, password, CancellationToken.None);
        var counterpartyId = await TestDbSeeder.SeedCounterpartyAsync(
            _factory.Services,
            admin.CompanyId,
            name: $"High Contention Counterparty {Guid.NewGuid():N}",
            CancellationToken.None);

        using var client = _factory.CreateClient();

        // Login via HTTP.
        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { email = admin.Email, password = admin.Password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        var loginJson = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, loginJson.ValueKind);
        Assert.True(loginJson.TryGetProperty("token", out var tokenProp));
        var token = tokenProp.GetString();
        Assert.True(!string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Same idempotency key + same payload across all requests.
        var idempotencyKey = Guid.NewGuid().ToString();
        var contractName = $"HIGH-CONTENTION-{Guid.NewGuid():N}";
        var payload = new { name = contractName, counterpartyId };

        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, parallelism)
            .Select(async _ =>
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = JsonContent.Create(payload)
                };

                req.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

                var resp = await client.SendAsync(req);

                // No 500, no 409; only 200/201 allowed.
                Assert.True(resp.StatusCode != HttpStatusCode.InternalServerError);
                Assert.True(resp.StatusCode != HttpStatusCode.Conflict);
                Assert.True(resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.Created);

                var body = await resp.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<JsonElement>(body);
                Assert.Equal(JsonValueKind.Object, json.ValueKind);
                Assert.True(json.TryGetProperty("id", out var idProp));
                var id = idProp.GetGuid();
                Assert.NotEqual(Guid.Empty, id);
                return id;
            })
            .ToArray();

        var ids = await Task.WhenAll(tasks);

        sw.Stop();

        Assert.True(sw.Elapsed < maxDuration);

        Assert.Equal(parallelism, ids.Length);
        var expected = ids[0];
        Assert.NotEqual(Guid.Empty, expected);
        Assert.True(ids.All(x => x == expected));

        // Ensure only one contract exists in this tenant.
        var listResp = await client.GetAsync("/api/contracts");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var listJson = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, listJson.ValueKind);
        Assert.True(listJson.TryGetProperty("items", out var itemsProp));
        Assert.Equal(JsonValueKind.Array, itemsProp.ValueKind);
        Assert.Equal(1, itemsProp.GetArrayLength());

        var item0 = itemsProp[0];
        Assert.True(item0.TryGetProperty("id", out var listedIdProp));
        Assert.Equal(expected, listedIdProp.GetGuid());
    }
}

