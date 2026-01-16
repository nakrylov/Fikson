using Fixon.Domain.Abstractions;

namespace Fixon.Domain.Imports;

/// <summary>
/// ImportBatch — юридически значимая партия импорта (two-phase: validate → commit).
/// Raw строки сохраняются в ImportRow. Batch не удаляется.
/// </summary>
public sealed class ImportBatch : TenantEntity
{
    public ImportSource Source { get; private set; }
    public string? ExternalBatchId { get; private set; }
    public ImportBatchStatus Status { get; private set; }
    public string Checksum { get; private set; } = null!;
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    private ImportBatch() { } // EF

    public ImportBatch(
        Guid id,
        Guid companyId,
        ImportSource source,
        string? externalBatchId,
        string checksum,
        DateTimeOffset startedAt)
    {
        if (string.IsNullOrWhiteSpace(checksum)) throw new ArgumentException(nameof(checksum));

        Id = id;
        CompanyId = companyId;
        Source = source;
        ExternalBatchId = externalBatchId;
        Checksum = checksum;
        StartedAt = startedAt;
        Status = ImportBatchStatus.Uploaded;
    }

    public void MarkValidated(DateTimeOffset atUtc)
    {
        if (Status != ImportBatchStatus.Uploaded) throw new InvalidOperationException("Batch must be Uploaded to become Validated.");
        Status = ImportBatchStatus.Validated;
    }

    public void MarkCommitted(DateTimeOffset atUtc)
    {
        if (Status != ImportBatchStatus.Validated) throw new InvalidOperationException("Batch must be Validated to commit.");
        Status = ImportBatchStatus.Committed;
        FinishedAt = atUtc;
    }

    public void MarkFailed(DateTimeOffset atUtc)
    {
        if (Status == ImportBatchStatus.Committed) throw new InvalidOperationException("Committed batch cannot fail.");
        Status = ImportBatchStatus.Failed;
        FinishedAt = atUtc;
    }
}

