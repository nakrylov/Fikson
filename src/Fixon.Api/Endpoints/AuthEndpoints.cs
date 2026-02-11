using Fixon.Domain.Users;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        // Получаем роли пользователя
        var roles = user.UserRoles
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role!.Name)
            .ToList();

        // Получаем permissions для ролей
        var allPermissions = new HashSet<string>();
        foreach (var role in roles)
        {
            var rolePermissions = RolePermissionsMapping.GetPermissionsForRole(role);
            foreach (var permission in rolePermissions)
            {
                allPermissions.Add(permission);
            }
        }

        // Генерируем JWT токен
        var token = tokenService.GenerateToken(user, roles, allPermissions);

        return Results.Ok(new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            TenantId = user.CompanyId,
            Roles = roles,
            Permissions = allPermissions.ToList(),
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

