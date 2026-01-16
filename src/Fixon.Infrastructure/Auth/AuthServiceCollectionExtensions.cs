using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Fixon.Infrastructure.Auth;

/// <summary>
/// Extension methods для регистрации authentication и authorization в DI контейнере.
/// </summary>
public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует JWT authentication и authorization.
    /// </summary>
    public static IServiceCollection AddFixonAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Настройки JWT из конфигурации
        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings
        {
            SecretKey = configuration["JWT_SECRET_KEY"] ?? throw new InvalidOperationException("JWT_SECRET_KEY not configured"),
            Issuer = configuration["JWT_ISSUER"] ?? "Fixon",
            Audience = configuration["JWT_AUDIENCE"] ?? "Fixon",
            Expiration = TimeSpan.FromHours(1),
        };

        services.AddSingleton(jwtSettings);
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        // JWT Bearer Authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero, // Точное время без допуска
            };
        });

        // Authorization
        services.AddAuthorization(options =>
        {
            AuthorizationPolicies.AddAuthorizationPolicies(options);
        });

        return services;
    }
}

