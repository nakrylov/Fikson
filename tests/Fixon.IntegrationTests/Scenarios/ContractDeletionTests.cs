using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fixon.Domain.Contracts;
using Fixon.Infrastructure.Persistence;
using Fixon.Infrastructure.Tenancy;
using Fixon.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class ContractDeletionTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public ContractDeletionTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Delete_contract_is_authorized_idempotent_and_safe_under_concurrency()
    {
        // Local constants/values (as required)
        var createEndpoint = "/api/contracts";
        var password = "Password123!";

        // Bootstrap tenant + admin
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, password, CancellationToken.None);
        var counterpartyId = await SeedCounterpartyAsync(admin.CompanyId, "Deletion Test Counterparty");

        using var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client, admin.Email, admin.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // STEP 1: Create contract (valid CounterpartyId + Idempotency-Key)
        var idempotencyKey = Guid.NewGuid().ToString();
        var contractName = $"DELETE-CONTRACT-{Guid.NewGuid():N}";

        using (var createReq = new HttpRequestMessage(HttpMethod.Post, createEndpoint)
        {
            Content = JsonContent.Create(new { name = contractName, counterpartyId })
        })
        {
            createReq.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            var createResp = await client.SendAsync(createReq);
            Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

            var createJson = await createResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Object, createJson.ValueKind);
            Assert.True(createJson.TryGetProperty("id", out var idProp));
            var contractId = idProp.GetGuid();
            Assert.NotEqual(Guid.Empty, contractId);

            var getUrl = $"/api/contracts/{contractId}";

            // STEP 2: GET contract -> 200 OK
            var getBeforeDelete = await client.GetAsync(getUrl);
            Assert.Equal(HttpStatusCode.OK, getBeforeDelete.StatusCode);
            var etag = getBeforeDelete.Headers.ETag;
            Assert.True(etag is not null && !string.IsNullOrWhiteSpace(etag.Tag));

            // STEP 3: DELETE contract -> 204 NoContent or 200 OK
            var deleteResp1 = await client.DeleteAsync(getUrl);
            var step3Ok = deleteResp1.StatusCode == HttpStatusCode.NoContent || deleteResp1.StatusCode == HttpStatusCode.OK;
            Assert.True(step3Ok);

            // STEP 4: Repeat DELETE -> 404 or 204 (idempotent)
            var deleteResp2 = await client.DeleteAsync(getUrl);
            var step4Ok = deleteResp2.StatusCode == HttpStatusCode.NotFound || deleteResp2.StatusCode == HttpStatusCode.NoContent;
            Assert.True(step4Ok);

            // STEP 5: GET after delete -> 404 NotFound
            var getAfterDelete = await client.GetAsync(getUrl);
            Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);

            // STEP 6: PUT after delete with previously saved ETag
            using (var putReq = new HttpRequestMessage(HttpMethod.Put, getUrl)
            {
                Content = JsonContent.Create(new { name = $"PUT-AFTER-DELETE-{Guid.NewGuid():N}" })
            })
            {
                putReq.Headers.IfMatch.Add(new EntityTagHeaderValue(etag!.Tag));
                var putResp = await client.SendAsync(putReq);

                // Current behavior may be 404 (not found) or 412 (precondition failed).
                var step6Ok = putResp.StatusCode == HttpStatusCode.NotFound || putResp.StatusCode == HttpStatusCode.PreconditionFailed;
                Assert.True(step6Ok);
            }

            // STEP 7: Concurrency: create new contract, then parallel DELETEs
            var contractName2 = $"DELETE-CONTRACT-2-{Guid.NewGuid():N}";
            var idempotencyKey2 = Guid.NewGuid().ToString();

            Guid contractId2;
            using (var createReq2 = new HttpRequestMessage(HttpMethod.Post, createEndpoint)
            {
                Content = JsonContent.Create(new { name = contractName2, counterpartyId })
            })
            {
                createReq2.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey2);
                var createResp2 = await client.SendAsync(createReq2);
                Assert.Equal(HttpStatusCode.Created, createResp2.StatusCode);

                var createJson2 = await createResp2.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal(JsonValueKind.Object, createJson2.ValueKind);
                Assert.True(createJson2.TryGetProperty("id", out var id2Prop));
                contractId2 = id2Prop.GetGuid();
                Assert.NotEqual(Guid.Empty, contractId2);
            }

            var deleteUrl2 = $"/api/contracts/{contractId2}";
            using var delClientA = _factory.CreateClient();
            using var delClientB = _factory.CreateClient();
            delClientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            delClientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var t1 = delClientA.DeleteAsync(deleteUrl2);
            var t2 = delClientB.DeleteAsync(deleteUrl2);

            await Task.WhenAll(t1, t2);

            var r1 = await t1;
            var r2 = await t2;

            Assert.True(r1.StatusCode != HttpStatusCode.InternalServerError);
            Assert.True(r2.StatusCode != HttpStatusCode.InternalServerError);

            // One must be successful (204/200). The other may be 204 or 404 but must not be 500.
            var success1 = r1.StatusCode == HttpStatusCode.NoContent || r1.StatusCode == HttpStatusCode.OK;
            var success2 = r2.StatusCode == HttpStatusCode.NoContent || r2.StatusCode == HttpStatusCode.OK;
            Assert.True(success1 || success2);

            var allowedSecond = r1.StatusCode == HttpStatusCode.NoContent || r1.StatusCode == HttpStatusCode.NotFound || r1.StatusCode == HttpStatusCode.OK;
            Assert.True(allowedSecond);
            allowedSecond = r2.StatusCode == HttpStatusCode.NoContent || r2.StatusCode == HttpStatusCode.NotFound || r2.StatusCode == HttpStatusCode.OK;
            Assert.True(allowedSecond);
        }
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var payload = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.True(!string.IsNullOrWhiteSpace(payload!.Token));
        return payload.Token;
    }

    private async Task<Guid> SeedCounterpartyAsync(Guid tenantId, string name)
    {
        // Precondition helper (same approach as other tests): Contract requires existing Counterparty FK.
        await TestDbSeeder.EnsureMigratedAsync(_factory.Services, CancellationToken.None);

        using var scope = _factory.Services.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var cs = cfg.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("DefaultConnection is not configured for tests.");

        var options = new DbContextOptionsBuilder<FixonDbContext>()
            .UseNpgsql(cs)
            .Options;

        await using var db = new FixonDbContext(options, new SystemTenantProvider());

        var id = Guid.NewGuid();
        db.Counterparties.Add(new Counterparty(
            id: id,
            companyId: tenantId,
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

