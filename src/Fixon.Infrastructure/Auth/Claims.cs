using System.Security.Claims;

namespace Fixon.Infrastructure.Auth;

/// <summary>
/// Константы для JWT claims.
/// 
/// JWT содержит:
/// - sub (UserId)
/// - tenant_id (CompanyId)
/// - role
/// - permissions (массив)
/// </summary>
public static class FixonClaims
{
    /// <summary>
    /// User ID (subject claim).
    /// </summary>
    public const string UserId = ClaimTypes.NameIdentifier;

    /// <summary>
    /// Tenant ID (CompanyId).
    /// </summary>
    public const string TenantId = "tenant_id";

    /// <summary>
    /// Role claim.
    /// </summary>
    public const string Role = ClaimTypes.Role;

    /// <summary>
    /// Permissions claim (массив permissions).
    /// </summary>
    public const string Permissions = "permissions";
}

