using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Fixon.Infrastructure.Tenancy;

/// <summary>
/// Tenant provider, извлекающий TenantId из HTTP header "X-Tenant-Id".
/// Используется для API Key аутентификации или специальных интеграций.
/// 
/// ВАЖНО: В production должен быть защищён дополнительной валидацией (API Key, подпись).
/// </summary>
public sealed class HeaderTenantProvider : ITenantProvider
{
    private const string TenantIdHeaderName = "X-Tenant-Id";
    private const string ApiKeyHeaderName = "X-Api-Key";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public HeaderTenantProvider(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    public Guid? TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Request?.Headers == null)
            {
                return null;
            }

            // Secure-by-default: never allow header tenant for authenticated requests.
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                return null;
            }

            // Allow header tenant only for integrations protected by API key.
            var expectedApiKey = _configuration["Tenancy:HeaderTenantApiKey"];
            if (string.IsNullOrWhiteSpace(expectedApiKey))
            {
                return null; // header-based tenant resolution disabled
            }

            if (!httpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyValue) ||
                string.IsNullOrWhiteSpace(apiKeyValue))
            {
                return null;
            }

            if (!string.Equals(apiKeyValue.ToString(), expectedApiKey, StringComparison.Ordinal))
            {
                return null;
            }

            if (!httpContext.Request.Headers.TryGetValue(TenantIdHeaderName, out var headerValue))
            {
                return null;
            }

            var tenantIdString = headerValue.ToString();
            if (string.IsNullOrWhiteSpace(tenantIdString))
            {
                return null;
            }

            if (!Guid.TryParse(tenantIdString, out var tenantId))
            {
                return null;
            }

            return tenantId;
        }
    }

    public bool IsSystemContext => false;
}

