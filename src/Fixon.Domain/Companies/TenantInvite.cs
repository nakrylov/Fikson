using Fixon.Domain.Abstractions;
using Fixon.Domain.Users;

namespace Fixon.Domain.Companies;

/// <summary>
/// Tenant invite: binds an invitation token to a specific email and tenant.
/// Acceptance creates a UserTenantMembership.
/// </summary>
public sealed class TenantInvite : Entity
{
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = null!;
    public MembershipRole Role { get; private set; }

    public string Token { get; private set; } = null!;
    public InviteStatus Status { get; private set; } = InviteStatus.Pending;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }

    public Company? Tenant { get; private set; }

    private TenantInvite() { } // EF / serialization

    public TenantInvite(
        Guid id,
        Guid tenantId,
        string email,
        MembershipRole role,
        string token,
        InviteStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? acceptedAt = null)
    {
        Id = id;
        TenantId = tenantId;
        Email = email;
        Role = role;
        Token = token;
        Status = status;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        AcceptedAt = acceptedAt;
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public void MarkAccepted(DateTimeOffset now)
    {
        Status = InviteStatus.Accepted;
        AcceptedAt = now;
    }

    public void MarkRevoked()
    {
        Status = InviteStatus.Revoked;
    }
}

public enum InviteStatus
{
    Pending = 0,
    Accepted = 1,
    Expired = 2,
    Revoked = 3,
}

