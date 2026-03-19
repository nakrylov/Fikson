using Fixon.Application.Emails;

namespace Fixon.IntegrationTests.Infrastructure;

public sealed class TestEmailService : IEmailService
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
