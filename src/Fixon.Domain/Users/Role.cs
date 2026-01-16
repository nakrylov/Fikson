using Fixon.Domain.Abstractions;

namespace Fixon.Domain.Users;

public sealed class Role : Entity
{
    public string Name { get; private set; } = null!;

    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

    private Role() { } // EF / serialization

    public Role(Guid id, string name)
    {
        Id = id;
        Name = name;
    }
}


