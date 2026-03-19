using Fixon.Application.Emails;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Fixon.Infrastructure.Emails;

public sealed class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public SmtpEmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            throw new ArgumentException("Recipient email is required.", nameof(to));
        }

        var host = _configuration["Email:Host"];
        var portRaw = _configuration["Email:Port"];
        var user = _configuration["Email:User"];
        var password = _configuration["Email:Password"];
        var from = _configuration["Email:From"];
        var securityModeRaw = _configuration["Email:SecurityMode"];
        var timeoutMsRaw = _configuration["Email:TimeoutMs"];

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("Email:Host is not configured.");
        }

        if (!int.TryParse(portRaw, out var port) || port <= 0)
        {
            throw new InvalidOperationException("Email:Port is not configured or invalid.");
        }

        if (string.IsNullOrWhiteSpace(from))
        {
            throw new InvalidOperationException("Email:From is not configured.");
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var smtp = new SmtpClient();
        if (!int.TryParse(timeoutMsRaw, out var timeoutMs) || timeoutMs <= 0)
        {
            timeoutMs = 15000;
        }
        smtp.Timeout = timeoutMs;

        var socketOptions = ResolveSocketOptions(port, securityModeRaw);
        await smtp.ConnectAsync(host, port, socketOptions, ct);

        if (!string.IsNullOrWhiteSpace(user))
        {
            await smtp.AuthenticateAsync(user, password ?? string.Empty, ct);
        }

        await smtp.SendAsync(message, ct);
        await smtp.DisconnectAsync(true, ct);
    }

    private static SecureSocketOptions ResolveSocketOptions(int port, string? securityModeRaw)
    {
        if (!string.IsNullOrWhiteSpace(securityModeRaw))
        {
            if (string.Equals(securityModeRaw, "SslOnConnect", StringComparison.OrdinalIgnoreCase))
            {
                return SecureSocketOptions.SslOnConnect;
            }

            if (string.Equals(securityModeRaw, "StartTls", StringComparison.OrdinalIgnoreCase))
            {
                return SecureSocketOptions.StartTls;
            }

            if (string.Equals(securityModeRaw, "StartTlsWhenAvailable", StringComparison.OrdinalIgnoreCase))
            {
                return SecureSocketOptions.StartTlsWhenAvailable;
            }

            if (string.Equals(securityModeRaw, "None", StringComparison.OrdinalIgnoreCase))
            {
                return SecureSocketOptions.None;
            }
        }

        // Safe defaults: implicit SSL for 465, opportunistic STARTTLS otherwise.
        return port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;
    }
}
