using Fixon.Domain.Abstractions;
using Fixon.Domain.Companies;

namespace Fixon.Domain.Users;

public sealed class User : ActivatableTenantEntity
{
    public string Email { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public Company? Company { get; private set; }
    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

    private User() { } // EF / serialization

    public User(Guid id, Guid companyId, string email, string name, string passwordHash, DateTimeOffset createdAt, bool isActive = true)
    {
        Id = id;
        CompanyId = companyId;
        Email = email;
        Name = name;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
        IsActive = isActive;
    }
}


