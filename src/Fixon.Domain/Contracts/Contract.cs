using Fixon.Domain.Abstractions;
using Fixon.Domain.Companies;

namespace Fixon.Domain.Contracts;

public sealed class Contract : ActivatableTenantEntity
{
    public Guid CounterpartyId { get; private set; }
    public string Name { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public ContractStatus Status { get; private set; }
    public Guid? CurrentVersionId { get; private set; }

    public Company? Company { get; private set; }
    public Counterparty? Counterparty { get; private set; }

    public ICollection<ContractVersion> Versions { get; private set; } = new List<ContractVersion>();
    public ICollection<Sla.SlaRule> SlaRules { get; private set; } = new List<Sla.SlaRule>();

    private Contract() { } // EF / serialization

    public Contract(Guid id, Guid companyId, Guid counterpartyId, string name, DateTimeOffset createdAt, bool isActive = true)
    {
        Id = id;
        CompanyId = companyId;
        CounterpartyId = counterpartyId;
        Name = name;
        CreatedAt = createdAt;
        IsActive = isActive;
        Status = ContractStatus.Draft;
    }

    public void ActivateVersion(Guid contractVersionId)
    {
        if (Status == ContractStatus.Terminated) throw new InvalidOperationException("Contract is terminated.");
        if (contractVersionId == Guid.Empty) throw new ArgumentException(nameof(contractVersionId));

        CurrentVersionId = contractVersionId;
        Status = ContractStatus.Active;
    }

    public void Terminate()
    {
        Status = ContractStatus.Terminated;
        IsActive = false;
    }
}


