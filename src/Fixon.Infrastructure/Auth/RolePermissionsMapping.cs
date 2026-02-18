namespace Fixon.Infrastructure.Auth;

/// <summary>
/// Mapping ролей к permissions.
/// 
/// Роль → набор permissions.
/// Авторизация выполняется по permissions, а не по ролям.
/// </summary>
public static class RolePermissionsMapping
{
    /// <summary>
    /// Получает permissions для роли.
    /// </summary>
    public static IReadOnlySet<string> GetPermissionsForRole(string role)
    {
        return role switch
        {
            Roles.Pledgor => PledgorPermissions,
            Roles.ThreePL => ThreePLPermissions,
            Roles.Admin => AdminPermissions,
            // SaaS memberships (step 1): map membership roles to existing permission sets.
            // - Member ≈ Pledgor (full access within tenant)
            // - Viewer ≈ ThreePL (read/confirm limited access)
            "Member" => PledgorPermissions,
            "Viewer" => ThreePLPermissions,
            _ => throw new ArgumentException($"Unknown role: {role}", nameof(role))
        };
    }

    /// <summary>
    /// Проверяет, имеет ли роль указанный permission.
    /// </summary>
    public static bool RoleHasPermission(string role, string permission)
    {
        var permissions = GetPermissionsForRole(role);
        return permissions.Contains(permission);
    }

    // Pledgor (Поклажедатель) — полный доступ к своим данным
    private static readonly HashSet<string> PledgorPermissions = new()
    {
        Permissions.ContractsRead,
        Permissions.ContractsManage,
        Permissions.SlaView,
        Permissions.SlaManage,
        Permissions.FactsRead,
        Permissions.FactsUpload,
        Permissions.ClaimsRead,
        Permissions.ClaimsManage,
        Permissions.ImportsUpload,
        Permissions.AuditRead,
    };

    // ThreePL (3PL оператор) — ограниченный доступ
    private static readonly HashSet<string> ThreePLPermissions = new()
    {
        Permissions.FactsRead,
        Permissions.FactsUpload,
        Permissions.FactsConfirm,
        Permissions.SlaView,
        Permissions.SlaConfirm,
        Permissions.ImportsUpload,
        // НЕТ доступа к:
        // - ContractsManage (не видит финансовые условия)
        // - ClaimsManage (не видит претензии)
        // - AuditRead (ограниченный аудит)
    };

    // Admin (System Admin) — полный доступ + системные операции
    private static readonly HashSet<string> AdminPermissions = new()
    {
        // Все permissions Pledgor
        Permissions.ContractsRead,
        Permissions.ContractsManage,
        Permissions.SlaView,
        Permissions.SlaManage,
        Permissions.FactsRead,
        Permissions.FactsUpload,
        Permissions.ClaimsRead,
        Permissions.ClaimsManage,
        Permissions.ImportsUpload,
        Permissions.AuditRead,
        // + системные
        Permissions.UsersManage,
    };
}

