namespace Fixon.Infrastructure.Auth;

/// <summary>
/// Permissions для авторизации (предпочтительно над ролями).
/// 
/// Permissions:
/// - технические (не бизнес-логика)
/// - явно именованные
/// - используются в policy-based authorization
/// </summary>
public static class Permissions
{
    // Contracts
    public const string ContractsRead = "contracts.read";
    public const string ContractsManage = "contracts.manage";

    // SLA Rules
    public const string SlaView = "sla.view";
    public const string SlaManage = "sla.manage";
    public const string SlaConfirm = "sla.confirm";

    // Facts
    public const string FactsRead = "facts.read";
    public const string FactsUpload = "facts.upload";
    public const string FactsConfirm = "facts.confirm";

    // Claims
    public const string ClaimsRead = "claims.read";
    public const string ClaimsManage = "claims.manage";

    // Imports
    public const string ImportsUpload = "imports.upload";

    // Users (только для Admin)
    public const string UsersManage = "users.manage";

    // Audit
    public const string AuditRead = "audit.read";

    /// <summary>
    /// Все permissions системы.
    /// </summary>
    public static readonly string[] All = new[]
    {
        ContractsRead,
        ContractsManage,
        SlaView,
        SlaManage,
        SlaConfirm,
        FactsRead,
        FactsUpload,
        FactsConfirm,
        ClaimsRead,
        ClaimsManage,
        ImportsUpload,
        UsersManage,
        AuditRead,
    };
}

