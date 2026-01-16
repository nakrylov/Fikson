using Fixon.Domain.Abstractions;
using Fixon.Domain.Contracts;

namespace Fixon.Domain.Sla;

public sealed class SlaRule : ActivatableTenantEntity
{
    public Guid ContractId { get; private set; }
    public string Name { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public Contract? Contract { get; private set; }
    public ICollection<SlaRuleVersion> Versions { get; private set; } = new List<SlaRuleVersion>();

    private SlaRule() { } // EF / serialization

    public SlaRule(Guid id, Guid companyId, Guid contractId, string name, DateTimeOffset createdAt, bool isActive = true)
    {
        Id = id;
        CompanyId = companyId;
        ContractId = contractId;
        Name = name;
        CreatedAt = createdAt;
        IsActive = isActive;
    }
}


