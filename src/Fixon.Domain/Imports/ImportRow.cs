using Fixon.Domain.Abstractions;

namespace Fixon.Domain.Imports;

/// <summary>
/// ImportRow — сохранённая raw строка импорта + результат валидации.
/// </summary>
public sealed class ImportRow : TenantEntity
{
    public Guid ImportBatchId { get; private set; }
    public int RowNumber { get; private set; }
    public string RawPayload { get; private set; } = null!;
    public ImportRowValidationStatus ValidationStatus { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public ImportBatch? ImportBatch { get; private set; }

    private ImportRow() { } // EF

    public ImportRow(
        Guid id,
        Guid companyId,
        Guid importBatchId,
        int rowNumber,
        string rawPayload)
    {
        if (importBatchId == Guid.Empty) throw new ArgumentException(nameof(importBatchId));
        if (rowNumber <= 0) throw new ArgumentOutOfRangeException(nameof(rowNumber));
        if (string.IsNullOrWhiteSpace(rawPayload)) throw new ArgumentException(nameof(rawPayload));

        Id = id;
        CompanyId = companyId;
        ImportBatchId = importBatchId;
        RowNumber = rowNumber;
        RawPayload = rawPayload;
        ValidationStatus = ImportRowValidationStatus.Pending;
    }

    public void MarkValid()
    {
        ValidationStatus = ImportRowValidationStatus.Valid;
        ErrorCode = null;
        ErrorMessage = null;
    }

    public void MarkInvalid(string errorCode, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorCode)) throw new ArgumentException(nameof(errorCode));
        if (string.IsNullOrWhiteSpace(errorMessage)) throw new ArgumentException(nameof(errorMessage));

        ValidationStatus = ImportRowValidationStatus.Invalid;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public void MarkDuplicate(string errorMessage = "Duplicate fact")
    {
        ValidationStatus = ImportRowValidationStatus.Duplicate;
        ErrorCode = "DUPLICATE";
        ErrorMessage = errorMessage;
    }
}

