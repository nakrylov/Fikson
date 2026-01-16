using Fixon.Domain.Abstractions;
using Fixon.Domain.Companies;
using Fixon.Domain.Contracts;

namespace Fixon.Domain.Facts;

public sealed class Fact : TenantEntity
{
    public Guid? ContractId { get; private set; }
    public string? ExternalReference { get; private set; }
    public string FactType { get; private set; } = null!;
    public string AttributesJson { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Link to import batch (for replay safety / audit). Null for system-generated facts.
    /// </summary>
    public Guid? ImportBatchId { get; private set; }

    /// <summary>
    /// Hash of raw payload (diagnostics). Null for system-generated facts.
    /// </summary>
    public string? PayloadHash { get; private set; }

    public Company? Company { get; private set; }
    public Contract? Contract { get; private set; }

    private Fact() { } // EF / serialization

    public Fact(
        Guid id,
        Guid companyId,
        Guid? contractId,
        string? externalReference,
        string factType,
        string attributesJson,
        DateTimeOffset occurredAt,
        DateTimeOffset createdAt,
        Guid? importBatchId = null,
        string? payloadHash = null)
    {
        Id = id;
        CompanyId = companyId;
        ContractId = contractId;
        ExternalReference = externalReference;
        FactType = factType;
        AttributesJson = attributesJson;
        OccurredAt = occurredAt;
        CreatedAt = createdAt;
        ImportBatchId = importBatchId;
        PayloadHash = payloadHash;
    }
}


