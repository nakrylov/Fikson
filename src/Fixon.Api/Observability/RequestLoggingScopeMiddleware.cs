using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Tenancy;
using System.Security.Claims;

namespace Fixon.Api.Observability;

/// <summary>
/// Structured logging scope with tenant-aware + correlation-aware fields.
/// Запрещено логировать PII: здесь только идентификаторы и технический контекст.
/// </summary>
public sealed class RequestLoggingScopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingScopeMiddleware> _logger;

    public RequestLoggingScopeMiddleware(RequestDelegate next, ILogger<RequestLoggingScopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
    {
        var correlationId = context.Response.Headers.TryGetValue(CorrelationIdMiddleware.HeaderName, out var existing)
            ? existing.ToString()
            : (context.Request.Headers.TryGetValue(CorrelationIdMiddleware.HeaderName, out var incoming) ? incoming.ToString() : null);

        var tenantId = tenantProvider.TenantId?.ToString();

        var userId = context.User?.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(FixonClaims.UserId)?.Value
            : null;

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["tenantId"] = tenantId,
            ["correlationId"] = correlationId,
            ["operation"] = $"{context.Request.Method} {context.Request.Path}",
            ["userId"] = userId,
        }))
        {
            await _next(context);
        }
    }
}

