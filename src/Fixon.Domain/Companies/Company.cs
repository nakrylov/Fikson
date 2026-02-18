using Fixon.Domain.Abstractions;

namespace Fixon.Domain.Companies;

public sealed class Company : Entity
{
    public string Name { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Multi-tenant SaaS step 1: tenant memberships.
    public ICollection<Fixon.Domain.Users.UserTenantMembership> Memberships { get; private set; } =
        new List<Fixon.Domain.Users.UserTenantMembership>();

    private Company() { } // EF / serialization

    public Company(Guid id, string name, DateTimeOffset createdAt, bool isActive = true)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
        IsActive = isActive;
    }
}


