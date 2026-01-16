using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Fixon.Domain.Users;
using Microsoft.IdentityModel.Tokens;

namespace Fixon.Infrastructure.Auth;

/// <summary>
/// Сервис для генерации JWT токенов.
/// 
/// JWT содержит:
/// - sub (UserId)
/// - tenant_id (CompanyId)
/// - role
/// - permissions (массив)
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Генерирует JWT токен для пользователя.
    /// </summary>
    string GenerateToken(User user, IReadOnlyCollection<string> roles, IReadOnlySet<string> permissions);

    /// <summary>
    /// Валидирует и извлекает claims из JWT токена.
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtTokenService(JwtSettings settings)
    {
        _settings = settings;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public string GenerateToken(User user, IReadOnlyCollection<string> roles, IReadOnlySet<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(FixonClaims.UserId, user.Id.ToString()),
            new(FixonClaims.TenantId, user.CompanyId.ToString()),
        };

        // Добавляем роли
        foreach (var role in roles)
        {
            claims.Add(new Claim(FixonClaims.Role, role));
        }

        // Добавляем permissions
        foreach (var permission in permissions)
        {
            claims.Add(new Claim(FixonClaims.Permissions, permission));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(_settings.Expiration),
            signingCredentials: credentials);

        return _tokenHandler.WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero, // Точное время без допуска
            };

            return _tokenHandler.ValidateToken(token, validationParameters, out _);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Настройки JWT.
/// </summary>
public sealed class JwtSettings
{
    public string SecretKey { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(1);
}

