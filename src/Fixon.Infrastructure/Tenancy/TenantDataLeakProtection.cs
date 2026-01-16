using Fixon.Domain.Abstractions;

namespace Fixon.Infrastructure.Tenancy;

/// <summary>
/// Защита от утечек данных между tenant'ами.
/// Валидирует, что операции выполняются в правильном tenant контексте.
/// </summary>
public static class TenantDataLeakProtection
{
    /// <summary>
    /// Валидирует, что entity принадлежит текущему tenant.
    /// Выбрасывает исключение при попытке доступа к чужому tenant.
    /// </summary>
    public static void ValidateTenantAccess<TEntity>(TEntity entity, Guid? currentTenantId, bool isSystemContext)
        where TEntity : TenantEntity
    {
        if (isSystemContext)
        {
            return; // System context может обращаться к любым tenant
        }

        if (!currentTenantId.HasValue)
        {
            throw new InvalidOperationException(
                $"TenantId не определён. Невозможно проверить доступ к entity {typeof(TEntity).Name}.");
        }

        if (entity.CompanyId != currentTenantId.Value)
        {
            throw new UnauthorizedAccessException(
                $"Попытка доступа к entity {typeof(TEntity).Name} из другого tenant. " +
                $"Ожидался tenant {currentTenantId.Value}, получен {entity.CompanyId}.");
        }
    }

    /// <summary>
    /// Валидирует, что TenantId в операции совпадает с текущим tenant.
    /// Используется при создании/обновлении сущностей.
    /// </summary>
    public static void ValidateTenantId(Guid tenantId, Guid? currentTenantId, bool isSystemContext)
    {
        if (isSystemContext)
        {
            return; // System context может устанавливать любой tenant
        }

        if (!currentTenantId.HasValue)
        {
            throw new InvalidOperationException(
                "TenantId не определён. Невозможно создать/обновить сущность.");
        }

        if (tenantId != currentTenantId.Value)
        {
            throw new UnauthorizedAccessException(
                $"Попытка создать/обновить сущность с TenantId {tenantId}, " +
                $"но текущий tenant {currentTenantId.Value}.");
        }
    }

    /// <summary>
    /// Валидирует список entities на принадлежность текущему tenant.
    /// </summary>
    public static void ValidateTenantAccessBatch<TEntity>(
        IEnumerable<TEntity> entities,
        Guid? currentTenantId,
        bool isSystemContext)
        where TEntity : TenantEntity
    {
        if (isSystemContext)
        {
            return;
        }

        if (!currentTenantId.HasValue)
        {
            throw new InvalidOperationException(
                $"TenantId не определён. Невозможно проверить доступ к entities {typeof(TEntity).Name}.");
        }

        var wrongTenantEntities = entities
            .Where(e => e.CompanyId != currentTenantId.Value)
            .ToList();

        if (wrongTenantEntities.Any())
        {
            var wrongTenantIds = wrongTenantEntities
                .Select(e => e.CompanyId)
                .Distinct()
                .ToList();

            throw new UnauthorizedAccessException(
                $"Обнаружены entities {typeof(TEntity).Name} из других tenant'ов: {string.Join(", ", wrongTenantIds)}. " +
                $"Ожидался tenant {currentTenantId.Value}.");
        }
    }
}

