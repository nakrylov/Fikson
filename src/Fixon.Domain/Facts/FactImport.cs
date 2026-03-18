using Fixon.Domain.Abstractions;

namespace Fixon.Domain.Facts;

public sealed class FactImport : TenantEntity
{
    public string FileName { get; private set; } = null!;
    public int RowsImported { get; private set; }
    public int ClaimsGenerated { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private FactImport() { } // EF / serialization

    public FactImport(
        Guid id,
        Guid companyId,
        string fileName,
        int rowsImported,
        int claimsGenerated,
        DateTimeOffset createdAt,
        Guid createdByUserId)
    {
        Id = id;
        CompanyId = companyId;
        FileName = fileName;
        RowsImported = rowsImported;
        ClaimsGenerated = claimsGenerated;
        CreatedAt = createdAt;
        CreatedByUserId = createdByUserId;
    }
}
