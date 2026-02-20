using Fixon.Domain.Abstractions;

namespace Fixon.Domain.Companies;

public sealed class Company : Entity
{
    public string Name { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public PlanType PlanType { get; private set; } = PlanType.Free;
    public int MonthlyUsageLimit { get; private set; } = 1000;
    public bool IsActive { get; private set; } = true;

    // Multi-tenant SaaS step 1: tenant memberships.
    public ICollection<Fixon.Domain.Users.UserTenantMembership> Memberships { get; private set; } =
        new List<Fixon.Domain.Users.UserTenantMembership>();

    private Company() { } // EF / serialization

    public Company(Guid id, string name, DateTimeOffset createdAt, bool isActive = true, PlanType planType = PlanType.Free, int monthlyUsageLimit = 1000)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
        IsActive = isActive;
        PlanType = planType;
        MonthlyUsageLimit = monthlyUsageLimit;
    }
}

public enum PlanType
{
    Free = 0,
    Pro = 1,
}


