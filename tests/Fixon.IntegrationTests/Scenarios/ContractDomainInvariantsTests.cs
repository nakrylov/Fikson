using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fixon.IntegrationTests.Infrastructure;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class ContractDomainInvariantsTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public ContractDomainInvariantsTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Contract_invariants_cannot_be_changed_via_put()
    {
        // All values are local (as required).
        var password = "Password123!";
        var createEndpoint = "/api/contracts";
        var listEndpoint = "/api/contracts";

        // Arrange: bootstrap tenant + admin, seed 2 counterparties.
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, password, CancellationToken.None);
        var counterpartyId1 = await TestDbSeeder.SeedCounterpartyAsync(
            _factory.Services,
            admin.CompanyId,
            name: $"Invariant Counterparty A {Guid.NewGuid():N}",
            CancellationToken.None);
        var counterpartyId2 = await TestDbSeeder.SeedCounterpartyAsync(
            _factory.Services,
            admin.CompanyId,
            name: $"Invariant Counterparty B {Guid.NewGuid():N}",
            CancellationToken.None);

        using var client = _factory.CreateClient();

        // STEP 1: Login Admin via HTTP.
        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { email = admin.Email, password = admin.Password });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        var loginJson = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, loginJson.ValueKind);
        Assert.True(loginJson.TryGetProperty("token", out var tokenProp));
        var token = tokenProp.GetString();
        Assert.True(!string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // STEP 2: Create Contract via POST /api/contracts.
        var idempotencyKey = Guid.NewGuid().ToString();
        var originalName = $"INVARIANTS-{Guid.NewGuid():N}";

        Guid contractId;
        using (var createReq = new HttpRequestMessage(HttpMethod.Post, createEndpoint)
        {
            Content = JsonContent.Create(new { name = originalName, counterpartyId = counterpartyId1 })
        })
        {
            createReq.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            var createResp = await client.SendAsync(createReq);
            Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

            var createdJson = await createResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Object, createdJson.ValueKind);
            Assert.True(createdJson.TryGetProperty("id", out var idProp));
            contractId = idProp.GetGuid();
            Assert.NotEqual(Guid.Empty, contractId);
        }

        // Capture tenantId from list (if returned by API).
        var listBefore = await client.GetAsync(listEndpoint);
        Assert.Equal(HttpStatusCode.OK, listBefore.StatusCode);
        var listBeforeJson = await listBefore.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, listBeforeJson.ValueKind);
        Assert.True(listBeforeJson.TryGetProperty("tenantId", out var tenantIdProp));
        var tenantIdFromApi = tenantIdProp.GetGuid();
        Assert.Equal(admin.CompanyId, tenantIdFromApi);

        // GET contract to capture ETag and baseline fields.
        var getUrl = $"/api/contracts/{contractId}";
        var getBefore = await client.GetAsync(getUrl);
        Assert.Equal(HttpStatusCode.OK, getBefore.StatusCode);
        var etag = getBefore.Headers.ETag;
        Assert.True(etag is not null && !string.IsNullOrWhiteSpace(etag.Tag));

        var getBeforeJson = await getBefore.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, getBeforeJson.ValueKind);
        Assert.True(getBeforeJson.TryGetProperty("id", out var getIdProp));
        Assert.Equal(contractId, getIdProp.GetGuid());
        Assert.True(getBeforeJson.TryGetProperty("name", out var getNameProp));
        Assert.Equal(originalName, getNameProp.GetString());
        Assert.True(getBeforeJson.TryGetProperty("counterpartyId", out var getCpProp));
        var originalCounterpartyId = getCpProp.GetGuid();
        Assert.Equal(counterpartyId1, originalCounterpartyId);
        Assert.True(getBeforeJson.TryGetProperty("status", out var getStatusProp));
        var originalStatus = getStatusProp.ValueKind == JsonValueKind.Number ? getStatusProp.GetInt32() : 0;

        // STEP 3: Attempt to change immutable fields via PUT.
        var updatedName = $"INVARIANTS-UPDATED-{Guid.NewGuid():N}";
        var attemptedTenantId = Guid.NewGuid();
        var attemptedIdChange = Guid.NewGuid();
        var attemptedStatus = 999;

        using (var putReq = new HttpRequestMessage(HttpMethod.Put, getUrl)
        {
            // UpdateContractRequest only has Name today, but we send extra fields to verify they are rejected/ignored.
            Content = JsonContent.Create(new
            {
                id = attemptedIdChange,
                contractId = attemptedIdChange,
                name = updatedName,
                counterpartyId = counterpartyId2,
                tenantId = attemptedTenantId,
                companyId = attemptedTenantId,
                isActive = false,
                status = attemptedStatus
            })
        })
        {
            putReq.Headers.IfMatch.Add(new EntityTagHeaderValue(etag!.Tag));
            var putResp = await client.SendAsync(putReq);

            // Correct behaviors:
            // - 204: update accepted but forbidden fields must be ignored
            // - 400/403/409: update rejected
            Assert.True(
                putResp.StatusCode == HttpStatusCode.NoContent ||
                putResp.StatusCode == HttpStatusCode.BadRequest ||
                putResp.StatusCode == HttpStatusCode.Forbidden ||
                putResp.StatusCode == HttpStatusCode.Conflict);

            var putAccepted = putResp.StatusCode == HttpStatusCode.NoContent;

            // STEP 4: GET after PUT. Must still exist and invariants must hold.
            var getAfter = await client.GetAsync(getUrl);
            Assert.Equal(HttpStatusCode.OK, getAfter.StatusCode);

            var getAfterJson = await getAfter.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Object, getAfterJson.ValueKind);
            Assert.True(getAfterJson.TryGetProperty("id", out var afterIdProp));
            Assert.Equal(contractId, afterIdProp.GetGuid());

            Assert.True(getAfterJson.TryGetProperty("counterpartyId", out var afterCpProp));
            Assert.Equal(originalCounterpartyId, afterCpProp.GetGuid());

            // name changes only if update accepted; otherwise must stay the same
            Assert.True(getAfterJson.TryGetProperty("name", out var afterNameProp));
            Assert.Equal(putAccepted ? updatedName : originalName, afterNameProp.GetString());

            // status must not be arbitrarily set by client payload.
            Assert.True(getAfterJson.TryGetProperty("status", out var afterStatusProp));
            var afterStatus = afterStatusProp.ValueKind == JsonValueKind.Number ? afterStatusProp.GetInt32() : 0;
            Assert.Equal(originalStatus, afterStatus);

            // Still active: must be visible in list and belong to same tenantId.
            var listAfter = await client.GetAsync(listEndpoint);
            Assert.Equal(HttpStatusCode.OK, listAfter.StatusCode);

            var listAfterJson = await listAfter.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(listAfterJson.TryGetProperty("tenantId", out var tenantIdAfterProp));
            Assert.Equal(tenantIdFromApi, tenantIdAfterProp.GetGuid());

            Assert.True(listAfterJson.TryGetProperty("items", out var itemsProp));
            Assert.Equal(JsonValueKind.Array, itemsProp.ValueKind);
            var matchCount = itemsProp.EnumerateArray().Count(x =>
                x.TryGetProperty("id", out var xid) && xid.GetGuid() == contractId);
            Assert.Equal(1, matchCount);
        }

        // STEP 5: Separate attempt to change contractId via body (if DTO allows) must not change resource identity.
        // (We already included id/contractId fields above; this step asserts invariant again.)
        var getFinal = await client.GetAsync(getUrl);
        Assert.Equal(HttpStatusCode.OK, getFinal.StatusCode);
        var getFinalJson = await getFinal.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(getFinalJson.TryGetProperty("id", out var finalIdProp));
        Assert.Equal(contractId, finalIdProp.GetGuid());
    }
}

