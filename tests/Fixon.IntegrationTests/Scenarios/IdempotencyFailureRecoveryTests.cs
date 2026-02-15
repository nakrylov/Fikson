using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fixon.IntegrationTests.Infrastructure;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class IdempotencyFailureRecoveryTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public IdempotencyFailureRecoveryTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Idempotency_recovers_after_controlled_failure_and_does_not_create_duplicates()
    {
        // All values are local to the test method (as required).
        var password = "Password123!";
        var createEndpoint = "/api/contracts";
        var listEndpoint = "/api/contracts";
        var debugHeaderName = "X-Debug-Fail-After-Idempotency";

        // Arrange: bootstrap tenant + admin, seed required counterparty (FK precondition).
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, password, CancellationToken.None);
        var counterpartyId = await TestDbSeeder.SeedCounterpartyAsync(
            _factory.Services,
            admin.CompanyId,
            name: $"Idempotency Failure Recovery Counterparty {Guid.NewGuid():N}",
            CancellationToken.None);

        using var client = _factory.CreateClient();

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { email = admin.Email, password = admin.Password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        var loginJson = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, loginJson.ValueKind);
        Assert.True(loginJson.TryGetProperty("token", out var tokenProp));
        var token = tokenProp.GetString();
        Assert.True(!string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var idempotencyKey = Guid.NewGuid().ToString();
        var contractName = $"IDEMPOTENCY-FAIL-RECOVERY-{Guid.NewGuid():N}";
        var payload = new { name = contractName, counterpartyId };

        // STEP 2: First POST with debug header => controlled failure (5xx expected).
        using (var failReq = new HttpRequestMessage(HttpMethod.Post, createEndpoint)
        {
            Content = JsonContent.Create(payload)
        })
        {
            failReq.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            failReq.Headers.TryAddWithoutValidation(debugHeaderName, "true");

            var failResp = await client.SendAsync(failReq);
            Assert.True((int)failResp.StatusCode >= 500);
        }

        // STEP 3: Retry same POST (same Idempotency-Key), without debug header => success.
        HttpStatusCode successStatus;
        string successBody;
        JsonElement successJson;
        Guid contractId;

        using (var createReq = new HttpRequestMessage(HttpMethod.Post, createEndpoint)
        {
            Content = JsonContent.Create(payload)
        })
        {
            createReq.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

            var createResp = await client.SendAsync(createReq);
            Assert.True(createResp.StatusCode == HttpStatusCode.OK || createResp.StatusCode == HttpStatusCode.Created);

            successStatus = createResp.StatusCode;
            successBody = await createResp.Content.ReadAsStringAsync();

            successJson = JsonSerializer.Deserialize<JsonElement>(successBody);
            Assert.Equal(JsonValueKind.Object, successJson.ValueKind);
            Assert.True(successJson.TryGetProperty("id", out var idProp));
            contractId = idProp.GetGuid();
            Assert.NotEqual(Guid.Empty, contractId);
        }

        // STEP 4: Third POST with same Idempotency-Key => must return stored response (no duplicate).
        using (var replayReq = new HttpRequestMessage(HttpMethod.Post, createEndpoint)
        {
            Content = JsonContent.Create(payload)
        })
        {
            replayReq.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

            var replayResp = await client.SendAsync(replayReq);
            Assert.Equal(successStatus, replayResp.StatusCode);

            var replayBody = await replayResp.Content.ReadAsStringAsync();
            var replayJson = JsonSerializer.Deserialize<JsonElement>(replayBody);
            Assert.True(JsonElement.DeepEquals(successJson, replayJson));

            Assert.True(replayJson.TryGetProperty("id", out var replayIdProp));
            Assert.Equal(contractId, replayIdProp.GetGuid());
        }

        // STEP 5: Verify via GET list that exactly one contract exists with this id (in this tenant).
        var listResp = await client.GetAsync(listEndpoint);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var listJson = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, listJson.ValueKind);
        Assert.True(listJson.TryGetProperty("items", out var itemsProp));
        Assert.Equal(JsonValueKind.Array, itemsProp.ValueKind);

        Assert.Equal(1, itemsProp.GetArrayLength());
        var item0 = itemsProp[0];
        Assert.True(item0.TryGetProperty("id", out var listedIdProp));
        Assert.Equal(contractId, listedIdProp.GetGuid());
    }
}

