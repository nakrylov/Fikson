namespace Fixon.Infrastructure.Tenancy;

/// <summary>
/// Tenant provider для фоновых задач/ручных операций, где tenant известен заранее.
/// Это НЕ ambient/static контекст: просто конкретная реализация ITenantProvider.
/// </summary>
public sealed class FixedTenantProvider : ITenantProvider
{
    public FixedTenantProvider(Guid? tenantId, bool isSystemContext)
    {
        if (!isSystemContext && tenantId == null)
        {
            throw new ArgumentException("TenantId is required for non-system context.", nameof(tenantId));
        }

        TenantId = tenantId;
        IsSystemContext = isSystemContext;
    }

    public Guid? TenantId { get; }
    public bool IsSystemContext { get; }
}

