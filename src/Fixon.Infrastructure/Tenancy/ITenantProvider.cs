namespace Fixon.Infrastructure.Tenancy;

/// <summary>
/// Provider для получения TenantId текущего запроса/операции.
/// TenantId = CompanyId (см. docs/04-auth-multitenancy.md).
/// 
/// Важно:
/// - Provider scoped (per request)
/// - Fail-fast, если tenant не определён (кроме SystemContext)
/// - Domain не знает про источник TenantId
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// TenantId текущего контекста.
    /// Для SystemContext может быть null (bypass tenant filter).
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// Признак системного контекста (миграции, фоновые задачи, админ).
    /// В SystemContext tenant filter отключается.
    /// </summary>
    bool IsSystemContext { get; }
}

