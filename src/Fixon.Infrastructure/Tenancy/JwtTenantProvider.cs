using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Fixon.Infrastructure.Tenancy;

/// <summary>
/// Tenant provider, извлекающий TenantId из JWT claims.
/// Использует claim "companyId" (см. docs/04-auth-multitenancy.md).
/// </summary>
public sealed class JwtTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JwtTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var companyIdClaim = httpContext.User.FindFirst(Fixon.Infrastructure.Auth.FixonClaims.TenantId)?.Value;

            if (string.IsNullOrWhiteSpace(companyIdClaim))
            {
                return null;
            }

            if (!Guid.TryParse(companyIdClaim, out var tenantId))
            {
                return null;
            }

            return tenantId;
        }
    }

    public bool IsSystemContext => false;
}

