using System.Net;
using FluentAssertions;
using Fixon.IntegrationTests.Infrastructure;
using Xunit;

namespace Fixon.IntegrationTests.Scenarios;

public sealed class AuthUnauthorizedTests
    : IClassFixture<FixonWebApplicationFactory>
{
    private readonly FixonWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthUnauthorizedTests(FixonWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Protected_endpoint_returns_401_when_called_without_jwt()
    {
        // Do NOT set Authorization header.
        var response = await _client.GetAsync("/api/contracts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

