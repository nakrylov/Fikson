using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Fixon.IntegrationTests.Infrastructure;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class ValidationTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;

    public ValidationTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_contract_with_invalid_model_returns_400_with_validation_errors()
    {
        // Arrange: login as Admin via real flow
        var admin = await TestDbSeeder.SeedAdminAsync(_factory.Services, password: "Password123!", CancellationToken.None);

        using var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = admin.Email,
            password = admin.Password
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();
        login!.Token.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("n"));

        // Act: send an invalid contract model (empty Name + empty CounterpartyId)
        var createResponse = await client.PostAsJsonAsync("/api/contracts", new
        {
            name = "",
            counterpartyId = Guid.Empty
        });

        // Assert: 400 BadRequest
        createResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert: has validation-problem style payload with errors
        var payload = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        payload.ValueKind.Should().Be(JsonValueKind.Object);

        payload.TryGetProperty("errors", out var errorsProp).Should().BeTrue();
        errorsProp.ValueKind.Should().Be(JsonValueKind.Object);

        errorsProp.TryGetProperty("Name", out _).Should().BeTrue();
        errorsProp.TryGetProperty("CounterpartyId", out _).Should().BeTrue();
    }

    private sealed class LoginResponse
    {
        public string Token { get; set; } = default!;
    }
}

