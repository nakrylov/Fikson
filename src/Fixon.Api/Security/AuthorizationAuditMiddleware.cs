using Fixon.Infrastructure.Audit;
using Fixon.Infrastructure.Auth;
using System.Security.Claims;
using System.Text.Json;

namespace Fixon.Api.Security;

/// <summary>
/// Writes security-relevant audit entries based on HTTP outcomes (e.g., 403 Forbidden).
/// Framework-stable approach (does not depend on internal auth middleware handlers).
/// </summary>
public sealed class AuthorizationAuditMiddleware
{
    private readonly RequestDelegate _next;

    public AuthorizationAuditMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuditWriter audit)
    {
        await _next(context);

        // Only audit forbidden access for authenticated users (tenant-aware).
        if (context.Response.StatusCode != StatusCodes.Status403Forbidden)
        {
            return;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var tenantIdClaim = context.User.FindFirst(FixonClaims.TenantId)?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return;
        }

        var correlationId = context.Request.Headers["X-Correlation-Id"].ToString();
        if (string.IsNullOrWhiteSpace(correlationId)) correlationId = null;

        // Best-effort: never fail the request.
        try
        {
            await audit.WriteAsync(new AuditWriteRequest(
                CompanyId: tenantId,
                EntityType: "SecurityEvent",
                EntityId: $"{context.Request.Method} {context.Request.Path}",
                Action: "Forbidden",
                CorrelationId: correlationId,
                DetailsJson: JsonSerializer.Serialize(new
                {
                    method = context.Request.Method,
                    path = context.Request.Path.Value,
                    statusCode = context.Response.StatusCode
                })), context.RequestAborted);
        }
        catch
        {
            // swallow
        }
    }
}

