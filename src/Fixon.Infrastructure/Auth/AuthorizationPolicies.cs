using Microsoft.AspNetCore.Authorization;

namespace Fixon.Infrastructure.Auth;

/// <summary>
/// Authorization policies для policy-based authorization.
/// 
/// Политики проверяют permissions и сверяют tenant context.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Регистрирует все authorization policies.
    /// </summary>
    public static void AddAuthorizationPolicies(AuthorizationOptions options)
    {
        // Contracts
        options.AddPolicy(Permissions.ContractsRead, policy =>
            policy.RequireAssertion(context => HasPermission(context, Permissions.ContractsRead)));

        options.AddPolicy(Permissions.ContractsManage, policy =>
            policy.RequireAssertion(context => HasPermission(context, Permissions.ContractsManage)));

        // SLA
        options.AddPolicy(Permissions.SlaView, policy =>
            policy.RequireAssertion(context => HasPermission(context, Permissions.SlaView)));

        options.AddPolicy(Permissions.SlaManage, policy =>
            policy.RequireAssertion(context => HasPermission(context, Permissions.SlaManage)));

        options.AddPolicy(Permissions.SlaConfirm, policy =>
            policy.RequireAssertion(context => HasPermission(context, Permissions.SlaConfirm)));

        // Facts
        options.AddPolicy(Permissions.FactsRead, policy =>
            policy.RequireAssertion(context => HasPermission(context, Permissions.FactsRead)));

        options.AddPolicy(Permissions.FactsUpload, policy =>
            policy.RequireAssertion(context => HasPermission(context, Permissions.FactsUpload)));

        options.AddPolicy(Permissions.FactsConfirm, policy =>
            policy.RequireAssertion(context => HasPermission(context, Permissions.FactsConfirm)));

        // Claims
        options.AddPolicy(Permissions.ClaimsRead, policy =>
            policy.RequireAssertion(context => HasPermission(context, Permissions.ClaimsRead)));

        options.AddPolicy(Permissions.ClaimsManage, policy =>
            policy.RequireAssertion(context => HasPermission(context, Permissions.ClaimsManage)));

        // Imports
        options.AddPolicy(Permissions.ImportsUpload, policy =>
            policy.RequireAssertion(context => HasPermission(context, Permissions.ImportsUpload)));

        // Users (Admin only)
        options.AddPolicy(Permissions.UsersManage, policy =>
            policy.RequireAssertion(context => HasPermission(context, Permissions.UsersManage)));

        // Audit
        options.AddPolicy(Permissions.AuditRead, policy =>
            policy.RequireAssertion(context => HasPermission(context, Permissions.AuditRead)));
    }

    private static bool HasPermission(AuthorizationHandlerContext context, string permission)
    {
        var permissions = context.User.FindAll(FixonClaims.Permissions)
            .Select(c => c.Value)
            .ToHashSet();

        return permissions.Contains(permission);
    }
}

