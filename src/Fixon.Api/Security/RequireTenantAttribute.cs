namespace Fixon.Api.Security;

/// <summary>
/// Marker metadata: endpoint requires tenant context (tenant_id claim) and active membership.
/// Used by middleware to enforce Model B "tenant-less" JWT support.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireTenantAttribute : Attribute
{
}

