using Fixon.Domain.Abstractions;
using Fixon.Domain.Companies;

namespace Fixon.Domain.Users;

/// <summary>
/// Multi-tenant SaaS (step 1):
/// membership relation between a User and a Tenant (Company).
/// </summary>
public sealed class UserTenantMembership : Entity
{
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }

    public MembershipRole Role { get; private set; }
    public MembershipStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public User? User { get; private set; }
    public Company? Tenant { get; private set; }

    private UserTenantMembership() { } // EF / serialization

    public UserTenantMembership(
        Guid id,
        Guid userId,
        Guid tenantId,
        MembershipRole role,
        MembershipStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        TenantId = tenantId;
        Role = role;
        Status = status;
        CreatedAt = createdAt;
    }
}

public enum MembershipRole
{
    Admin = 0,
    Member = 1,
    Viewer = 2,
}

public enum MembershipStatus
{
    Active = 0,
    PendingInvite = 1,
    Suspended = 2,
}

