using System.Security.Cryptography;
using System.Text;

namespace Fixon.Infrastructure.Auth;

/// <summary>
/// Сервис для хеширования паролей.
/// Использует PBKDF2 (см. docs/04-auth-multitenancy.md).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Хеширует пароль.
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Проверяет пароль против хеша.
    /// </summary>
    bool VerifyPassword(string password, string passwordHash);
}

public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16; // 128 bits
    private const int HashSize = 32; // 256 bits
    private const int Iterations = 100000; // PBKDF2 iterations

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be empty.", nameof(password));
        }

        // Генерируем случайную соль
        var salt = new byte[SaltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Хешируем пароль с солью
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        // Объединяем соль и хеш в одну строку (base64)
        var saltAndHash = new byte[SaltSize + HashSize];
        Array.Copy(salt, 0, saltAndHash, 0, SaltSize);
        Array.Copy(hash, 0, saltAndHash, SaltSize, HashSize);

        return Convert.ToBase64String(saltAndHash);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            // Извлекаем соль и хеш из строки
            var saltAndHash = Convert.FromBase64String(passwordHash);
            if (saltAndHash.Length != SaltSize + HashSize)
            {
                return false;
            }

            var salt = new byte[SaltSize];
            var storedHash = new byte[HashSize];
            Array.Copy(saltAndHash, 0, salt, 0, SaltSize);
            Array.Copy(saltAndHash, SaltSize, storedHash, 0, HashSize);

            // Вычисляем хеш для введённого пароля
            var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            // Сравниваем хеши (constant-time comparison для защиты от timing attacks)
            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }
        catch
        {
            return false;
        }
    }
}

