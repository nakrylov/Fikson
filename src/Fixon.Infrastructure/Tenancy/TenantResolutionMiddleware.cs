using Microsoft.AspNetCore.Http;

namespace Fixon.Infrastructure.Tenancy;

/// <summary>
/// Middleware для разрешения tenant из HTTP запроса.
/// Устанавливает ITenantProvider в HttpContext для использования в DbContext и сервисах.
/// 
/// Fail-fast: возвращает 401/403, если tenant не определён (кроме публичных endpoints).
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _requireTenant;

    public TenantResolutionMiddleware(RequestDelegate next, bool requireTenant = true)
    {
        _next = next;
        _requireTenant = requireTenant;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        ITenantProvider tenantProvider)
    {
        // Публичные endpoints (health, login) могут не требовать tenant
        if (!_requireTenant || IsPublicEndpoint(httpContext))
        {
            await _next(httpContext);
            return;
        }

        // Проверяем наличие tenant (JWT tenant_id или интеграционный X-Api-Key + X-Tenant-Id)
        if (!tenantProvider.IsSystemContext && !tenantProvider.TenantId.HasValue)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    error = "TenantId не определён. Требуется аутентификация (JWT tenant_id) или интеграционный X-Api-Key + X-Tenant-Id."
                }));
            return;
        }

        await _next(httpContext);
    }

    private static bool IsPublicEndpoint(HttpContext httpContext)
    {
        var path = httpContext.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        return path == "/health" || path.StartsWith("/api/auth/login");
    }
}

