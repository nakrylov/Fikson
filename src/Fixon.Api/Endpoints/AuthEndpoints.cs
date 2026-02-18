using Fixon.Domain.Users;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Fixon.Api.Endpoints;

/// <summary>
/// Endpoints для аутентификации.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// POST /api/auth/login
    /// Аутентификация пользователя по email и паролю.
    /// </summary>
    public static async Task<IResult> Login(
        [FromBody] LoginRequest request,
        FixonDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenService tokenService)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { error = "Email and password are required." });
        }

        // Ищем пользователя по email (без tenant filter для login)
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !user.IsActive)
        {
            return Results.Unauthorized();
        }

        // Проверяем пароль
        if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Results.Unauthorized();
        }

        // SaaS memberships: choose one active membership by default (first by CreatedAt).
        var membership = await dbContext.UserTenantMemberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == user.Id)
            .Where(m => m.Status == MembershipStatus.Active)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        if (membership is null)
        {
            // User exists but has no active tenant membership.
            return Results.Unauthorized();
        }

        var currentTenantId = membership.TenantId;
        var membershipRole = membership.Role.ToString(); // Admin / Member / Viewer

        // Получаем permissions для ролей
        var allPermissions = new HashSet<string>();
        foreach (var permission in RolePermissionsMapping.GetPermissionsForRole(membershipRole))
        {
            allPermissions.Add(permission);
        }

        var roles = new List<string> { membershipRole };

        // Генерируем JWT токен
        var token = tokenService.GenerateToken(user, currentTenantId, roles, allPermissions);

        return Results.Ok(new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            TenantId = currentTenantId,
            Roles = roles,
            Permissions = allPermissions.ToList(),
        });
    }

    /// <summary>
    /// POST /api/auth/switch-tenant
    /// Switches current tenant context for a user with multiple memberships.
    /// </summary>
    public static async Task<IResult> SwitchTenant(
        [FromBody] SwitchTenantRequest request,
        ClaimsPrincipal user,
        FixonDbContext dbContext,
        IJwtTokenService tokenService,
        CancellationToken ct)
    {
        if (request.TenantId == Guid.Empty)
        {
            return Results.BadRequest(new { error = "TenantId is required." });
        }

        var userIdClaim = user.FindFirst(FixonClaims.UserId)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        // Validate tenant exists.
        var tenantExists = await dbContext.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(c => c.Id == request.TenantId, ct);

        if (!tenantExists)
        {
            return Results.NotFound();
        }

        // Validate membership exists and is active (ignore tenant filters, since we may switch).
        var membership = await dbContext.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.UserId == userId && m.TenantId == request.TenantId, ct);

        if (membership is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (membership.Status != MembershipStatus.Active)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var dbUser = await dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(u => u.Id == userId, ct);

        var membershipRole = membership.Role.ToString();

        var permissions = RolePermissionsMapping.GetPermissionsForRole(membershipRole);
        var roles = new List<string> { membershipRole };

        var token = tokenService.GenerateToken(dbUser, membership.TenantId, roles, permissions);

        return Results.Ok(new LoginResponse
        {
            Token = token,
            UserId = dbUser.Id,
            TenantId = membership.TenantId,
            Roles = roles,
            Permissions = permissions.ToList(),
        });
    }
}

public sealed class LoginRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public sealed class LoginResponse
{
    public string Token { get; set; } = null!;
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
}

public sealed class SwitchTenantRequest
{
    public Guid TenantId { get; set; }
}

