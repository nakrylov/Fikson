namespace Fixon.Infrastructure.Tenancy;

/// <summary>
/// Явный tenant context для текущей операции.
/// Используется в Application / Infrastructure слоях.
/// 
/// Может быть overridden для system jobs, migrations, admin операций.
/// </summary>
public sealed class TenantContext
{
    public Guid? TenantId { get; }
    public bool IsSystemContext { get; }

    private TenantContext(Guid? tenantId, bool isSystemContext)
    {
        TenantId = tenantId;
        IsSystemContext = isSystemContext;
    }

    /// <summary>
    /// Создаёт tenant context для обычного запроса (не system).
    /// </summary>
    public static TenantContext ForTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        return new TenantContext(tenantId, isSystemContext: false);
    }

    /// <summary>
    /// Создаёт system context (bypass tenant filter).
    /// Используется для миграций, фоновых задач, админских операций.
    /// </summary>
    public static TenantContext System()
    {
        return new TenantContext(tenantId: null, isSystemContext: true);
    }

    /// <summary>
    /// Создаёт system context с явным TenantId (для админских операций над конкретным tenant).
    /// </summary>
    public static TenantContext SystemWithTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        return new TenantContext(tenantId, isSystemContext: true);
    }
}

