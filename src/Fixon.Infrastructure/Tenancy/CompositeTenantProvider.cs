namespace Fixon.Infrastructure.Tenancy;

/// <summary>
/// Composite tenant provider, пробующий несколько источников по порядку:
/// 1. JWT claims
/// 2. HTTP header X-Tenant-Id
/// 
/// Fail-fast: выбрасывает исключение, если tenant не определён (кроме SystemContext).
/// </summary>
public sealed class CompositeTenantProvider : ITenantProvider
{
    private readonly JwtTenantProvider _jwtProvider;
    private readonly HeaderTenantProvider _headerProvider;
    private readonly bool _allowMissingTenant;

    public CompositeTenantProvider(
        JwtTenantProvider jwtProvider,
        HeaderTenantProvider headerProvider,
        bool allowMissingTenant = false)
    {
        _jwtProvider = jwtProvider;
        _headerProvider = headerProvider;
        _allowMissingTenant = allowMissingTenant;
    }

    public Guid? TenantId
    {
        get
        {
            // Пробуем JWT первым (основной источник для аутентифицированных пользователей)
            var tenantId = _jwtProvider.TenantId;
            if (tenantId.HasValue)
            {
                return tenantId;
            }

            // Fallback на header (только для API Key / интеграций, см. HeaderTenantProvider)
            tenantId = _headerProvider.TenantId;
            if (tenantId.HasValue)
            {
                return tenantId;
            }

            // Fail-fast: если tenant не определён и не разрешено пропускать
            if (!_allowMissingTenant)
            {
                throw new InvalidOperationException(
                    "TenantId не определён. Требуется JWT claim 'tenant_id' (или интеграционный X-Api-Key + X-Tenant-Id).");
            }

            return null;
        }
    }

    public bool IsSystemContext => false;
}

