namespace Fixon.Infrastructure.Tenancy;

/// <summary>
/// System tenant provider для миграций, фоновых задач, админских операций.
/// Bypass tenant filter включён по умолчанию.
/// </summary>
public sealed class SystemTenantProvider : ITenantProvider
{
    private readonly Guid? _tenantId;

    /// <summary>
    /// Создаёт system provider без конкретного tenant (полный bypass).
    /// </summary>
    public SystemTenantProvider()
    {
        _tenantId = null;
    }

    /// <summary>
    /// Создаёт system provider с явным tenant (для админских операций над конкретным tenant).
    /// </summary>
    public SystemTenantProvider(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        _tenantId = tenantId;
    }

    public Guid? TenantId => _tenantId;
    public bool IsSystemContext => true;
}

