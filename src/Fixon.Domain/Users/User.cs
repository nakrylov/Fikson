using Fixon.Domain.Abstractions;
using Fixon.Domain.Companies;

namespace Fixon.Domain.Users;

/// <summary>
/// SaaS (Model B): User is a global identity and may exist without a tenant membership.
/// Tenant access is defined by <see cref="UserTenantMembership"/>.
/// </summary>
public sealed class User : Entity
{
    /// <summary>
    /// Legacy / convenience link to a "home" tenant. Can be null for tenant-less users.
    /// Do not use this for authorization decisions (use memberships).
    /// </summary>
    public Guid? CompanyId { get; private set; }

    public string Email { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool EmailConfirmed { get; private set; } = false;
    public bool IsActive { get; private set; } = true;

    public Company? Company { get; private set; }
    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();
    public ICollection<UserTenantMembership> Memberships { get; private set; } = new List<UserTenantMembership>();

    private User() { } // EF / serialization

    public User(Guid id, Guid? companyId, string email, string name, string passwordHash, DateTimeOffset createdAt, bool isActive = true, bool emailConfirmed = false)
    {
        Id = id;
        CompanyId = companyId;
        Email = email;
        Name = name;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
        IsActive = isActive;
        EmailConfirmed = emailConfirmed;
    }

    public void AssignCompany(Guid companyId)
    {
        if (!CompanyId.HasValue)
        {
            CompanyId = companyId;
        }
    }
}


