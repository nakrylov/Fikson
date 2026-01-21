using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using Fixon.IntegrationTests.Infrastructure;


public sealed class BootstrapScenarioTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BootstrapScenarioTests(FixonWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Bootstrap_creates_tenant_admin_and_audit()
    {
        // Arrange
        var request = new
        {
            companyName = "Integration Test Company",
            adminEmail = "admin@integration.test"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/bootstrap/init",
            request
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<BootstrapResponse>();

        payload.Should().NotBeNull();
        payload!.TenantId.Should().NotBeEmpty();
        payload.AdminUserId.Should().NotBeEmpty();
    }

    private sealed record BootstrapResponse(
        Guid TenantId,
        Guid AdminUserId
    );
}
