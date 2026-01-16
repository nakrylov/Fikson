using Fixon.Domain.Abstractions;
using Fixon.Domain.Companies;

namespace Fixon.Domain.Contracts;

public sealed class Counterparty : ActivatableTenantEntity
{
    public string Name { get; private set; } = null!;
    public string? ExternalCode { get; private set; }

    public Company? Company { get; private set; }

    private Counterparty() { } // EF / serialization

    public Counterparty(Guid id, Guid companyId, string name, string? externalCode, bool isActive = true)
    {
        Id = id;
        CompanyId = companyId;
        Name = name;
        ExternalCode = externalCode;
        IsActive = isActive;
    }
}


