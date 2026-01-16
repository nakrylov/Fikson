using System.Security.Claims;

namespace Fixon.Infrastructure.Auth;

/// <summary>
/// Контекст текущего пользователя для Application layer.
/// 
/// Доступен в сервисах, audit logging, rule evaluation.
/// Domain не знает про UserContext — он используется только в Application/Infrastructure.
/// </summary>
public sealed class UserContext
{
    public Guid UserId { get; }
    public Guid TenantId { get; }
    public IReadOnlyCollection<string> Roles { get; }
    public IReadOnlySet<string> Permissions { get; }

    private UserContext(Guid userId, Guid tenantId, IReadOnlyCollection<string> roles, IReadOnlySet<string> permissions)
    {
        UserId = userId;
        TenantId = tenantId;
        Roles = roles;
        Permissions = permissions;
    }

    /// <summary>
    /// Создаёт UserContext из ClaimsPrincipal (JWT).
    /// </summary>
    public static UserContext FromClaimsPrincipal(ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(FixonClaims.UserId)?.Value;
        var tenantIdClaim = principal.FindFirst(FixonClaims.TenantId)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("User ID claim not found or invalid.");
        }

        if (string.IsNullOrWhiteSpace(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            throw new InvalidOperationException("Tenant ID claim not found or invalid.");
        }

        var roles = principal.FindAll(FixonClaims.Role)
            .Select(c => c.Value)
            .ToList();

        var permissions = principal.FindAll(FixonClaims.Permissions)
            .Select(c => c.Value)
            .ToHashSet();

        return new UserContext(userId, tenantId, roles, permissions);
    }

    /// <summary>
    /// Проверяет, имеет ли пользователь указанный permission.
    /// </summary>
    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission);
    }

    /// <summary>
    /// Проверяет, имеет ли пользователь хотя бы одну из указанных permissions.
    /// </summary>
    public bool HasAnyPermission(params string[] permissions)
    {
        return permissions.Any(Permissions.Contains);
    }

    /// <summary>
    /// Проверяет, имеет ли пользователь указанную роль.
    /// </summary>
    public bool HasRole(string role)
    {
        return Roles.Contains(role);
    }

    /// <summary>
    /// Проверяет, является ли пользователь системным админом.
    /// </summary>
    public bool IsAdmin()
    {
        return HasRole(Fixon.Infrastructure.Auth.Roles.Admin);
    }
}

