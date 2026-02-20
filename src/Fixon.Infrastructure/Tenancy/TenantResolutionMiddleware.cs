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
        // Tenant-less JWT support:
        // tenant is required only for endpoints explicitly marked with RequireTenant metadata.
        if (!_requireTenant || !EndpointRequiresTenant(httpContext))
        {
            await _next(httpContext);
            return;
        }

        // Проверяем наличие tenant (JWT tenant_id или интеграционный X-Api-Key + X-Tenant-Id)
        if (!tenantProvider.IsSystemContext && !tenantProvider.TenantId.HasValue)
        {
            // If user is authenticated but has no tenant context (tenant-less JWT),
            // this is a forbidden access to tenant-aware endpoint.
            httpContext.Response.StatusCode =
                httpContext.User?.Identity?.IsAuthenticated == true
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status401Unauthorized;
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

    private static bool EndpointRequiresTenant(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        if (endpoint == null) return false;

        // AllowAnonymous endpoints never require tenant.
        if (endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null)
        {
            return false;
        }

        // RequireTenant marker is defined in Fixon.Api (referenced by Fixon.Api project),
        // but metadata type is available at runtime.
        return endpoint.Metadata.Any(m => string.Equals(m.GetType().FullName, "Fixon.Api.Security.RequireTenantAttribute", StringComparison.Ordinal));
    }
}

