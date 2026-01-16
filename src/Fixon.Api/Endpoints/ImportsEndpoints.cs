using Fixon.Api.Observability;
using Fixon.Api.Imports;
using Fixon.Domain.Facts;
using Fixon.Domain.Imports;
using Fixon.Infrastructure.Audit;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fixon.Api.Endpoints;

public static class ImportsEndpoints
{
    private const int MaxRowsPerBatch = 5000;

    // v1: implement Facts import only (docs/05 section 10 + v2 idempotency section 13.2)
    [Authorize(Policy = Permissions.ImportsUpload)]
    public static async Task<IResult> UploadFactsImport(
        [FromBody] UploadFactsImportRequest request,
        ClaimsPrincipal user,
        FixonDbContext db,
        IAuditWriter audit,
        CancellationToken ct)
    {
        var tenantIdClaim = user.FindFirst(FixonClaims.TenantId)?.Value;
        if (string.IsNullOrWhiteSpace(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return Results.BadRequest(new { error = "Tenant ID not found in token." });
        }

        var delimiter = string.IsNullOrWhiteSpace(request.Delimiter) ? "," : request.Delimiter!;

        IReadOnlyList<Dictionary<string, string>> parsed;
        try
        {
            parsed = CsvImportParser.Parse(request.Csv, delimiter);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = "CSV_PARSE_ERROR", message = ex.Message });
        }

        if (parsed.Count > MaxRowsPerBatch)
        {
            return Results.BadRequest(new
            {
                error = "BATCH_TOO_LARGE",
                message = $"Batch row limit exceeded: {parsed.Count} > {MaxRowsPerBatch}"
            });
        }

        var batchId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var checksum = Sha256Hex(request.Csv);

        var batch = new ImportBatch(
            id: batchId,
            companyId: tenantId,
            source: ImportSource.Csv,
            externalBatchId: request.ExternalBatchId,
            checksum: checksum,
            startedAt: now);

        db.ImportBatches.Add(batch);

        var rowNumber = 1;
        foreach (var row in parsed)
        {
            // Store raw payload as jsonb (no PII; raw facts are legal record)
            var raw = JsonSerializer.Serialize(row);
            db.ImportRows.Add(new ImportRow(
                id: Guid.NewGuid(),
                companyId: tenantId,
                importBatchId: batchId,
                rowNumber: rowNumber++,
                rawPayload: raw));
        }

        await db.SaveChangesAsync(ct);

        ImportMetrics.RowsUploaded.Add(parsed.Count);

        await audit.WriteAsync(new AuditWriteRequest(
            CompanyId: tenantId,
            EntityType: "ImportBatch",
            EntityId: batchId.ToString(),
            Action: "Upload",
            CorrelationId: batchId.ToString(),
            DetailsJson: JsonSerializer.Serialize(new
            {
                importType = "facts",
                source = "csv",
                checksum,
                rows = parsed.Count
            })), ct);

        return Results.Accepted($"/api/imports/{batchId}", new
        {
            batchId,
            status = batch.Status,
            checksum,
            rows = parsed.Count
        });
    }

    [Authorize(Policy = Permissions.ImportsUpload)]
    public static async Task<IResult> ValidateImport(
        [FromRoute] Guid batchId,
        ClaimsPrincipal user,
        FixonDbContext db,
        IAuditWriter audit,
        CancellationToken ct)
    {
        var tenantIdClaim = user.FindFirst(FixonClaims.TenantId)?.Value;
        if (string.IsNullOrWhiteSpace(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return Results.BadRequest(new { error = "Tenant ID not found in token." });
        }

        var batch = await db.ImportBatches.SingleOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return Results.NotFound();
        if (batch.Status != ImportBatchStatus.Uploaded)
        {
            return Results.BadRequest(new { error = "INVALID_STATE", message = "Batch must be Uploaded to validate." });
        }

        var rows = await db.ImportRows
            .Where(r => r.ImportBatchId == batchId)
            .OrderBy(r => r.RowNumber)
            .ToListAsync(ct);

        // Schema validation + build candidate idempotency keys
        var parsedRows = new List<(ImportRow row, FactCandidate? candidate)>(rows.Count);
        foreach (var r in rows)
        {
            var (candidate, errorCode, errorMessage) = TryParseFactRow(r.RawPayload);
            if (candidate == null)
            {
                r.MarkInvalid(errorCode!, errorMessage!);
            }
            else
            {
                r.MarkValid();
            }

            parsedRows.Add((r, candidate));
        }

        // Duplicates within batch (same externalReference + occurredAt)
        var keyGroups = parsedRows
            .Where(x => x.candidate != null)
            .GroupBy(x => (x.candidate!.ExternalReference, x.candidate!.OccurredAt))
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var g in keyGroups)
        {
            foreach (var item in g)
            {
                item.row.MarkDuplicate("Duplicate within batch (same ExternalReference + OccurredAt).");
            }
        }

        // Duplicates against committed facts (idempotency key) — fail validation (no partial commit)
        var candidates = parsedRows
            .Where(x => x.candidate != null && x.row.ValidationStatus == ImportRowValidationStatus.Valid)
            .Select(x => x.candidate!)
            .ToList();

        if (candidates.Count > 0)
        {
            var extRefs = candidates.Select(c => c.ExternalReference).Distinct().ToList();
            var min = candidates.Min(c => c.OccurredAt);
            var max = candidates.Max(c => c.OccurredAt);

            var existing = await db.Facts.AsNoTracking()
                .Where(f => f.ExternalReference != null &&
                            extRefs.Contains(f.ExternalReference) &&
                            f.OccurredAt >= min && f.OccurredAt <= max)
                .Select(f => new { f.ExternalReference, f.OccurredAt })
                .ToListAsync(ct);

            var existingKeys = existing
                .Select(x => (x.ExternalReference!, x.OccurredAt))
                .ToHashSet();

            foreach (var (row, candidate) in parsedRows)
            {
                if (candidate == null) continue;
                if (row.ValidationStatus != ImportRowValidationStatus.Valid) continue;

                if (existingKeys.Contains((candidate.ExternalReference, candidate.OccurredAt)))
                {
                    row.MarkDuplicate("Duplicate against existing committed fact (idempotency key conflict).");
                }
            }
        }

        var invalidCount = rows.Count(r => r.ValidationStatus == ImportRowValidationStatus.Invalid);
        var dupCount = rows.Count(r => r.ValidationStatus == ImportRowValidationStatus.Duplicate);
        var validCount = rows.Count(r => r.ValidationStatus == ImportRowValidationStatus.Valid);

        ImportMetrics.RowsValidated.Add(validCount);
        ImportMetrics.RowsInvalid.Add(invalidCount);
        ImportMetrics.RowsDuplicate.Add(dupCount);

        // Two-phase rule: only zero errors allows commit
        if (invalidCount > 0 || dupCount > 0)
        {
            batch.MarkFailed(DateTimeOffset.UtcNow);
        }
        else
        {
            batch.MarkValidated(DateTimeOffset.UtcNow);
        }

        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditWriteRequest(
            CompanyId: tenantId,
            EntityType: "ImportBatch",
            EntityId: batchId.ToString(),
            Action: "Validate",
            CorrelationId: batchId.ToString(),
            DetailsJson: JsonSerializer.Serialize(new
            {
                valid = validCount,
                invalid = invalidCount,
                duplicate = dupCount,
                status = batch.Status.ToString()
            })), ct);

        return Results.Ok(new
        {
            batchId,
            status = batch.Status,
            valid = validCount,
            invalid = invalidCount,
            duplicate = dupCount
        });
    }

    [Authorize(Policy = Permissions.ImportsUpload)]
    public static async Task<IResult> CommitImport(
        [FromRoute] Guid batchId,
        ClaimsPrincipal user,
        FixonDbContext db,
        IAuditWriter audit,
        CancellationToken ct)
    {
        var tenantIdClaim = user.FindFirst(FixonClaims.TenantId)?.Value;
        if (string.IsNullOrWhiteSpace(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return Results.BadRequest(new { error = "Tenant ID not found in token." });
        }

        var batch = await db.ImportBatches.SingleOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return Results.NotFound();

        if (batch.Status != ImportBatchStatus.Validated)
        {
            return Results.BadRequest(new { error = "INVALID_STATE", message = "Batch must be Validated to commit." });
        }

        var rows = await db.ImportRows
            .Where(r => r.ImportBatchId == batchId)
            .OrderBy(r => r.RowNumber)
            .ToListAsync(ct);

        if (rows.Any(r => r.ValidationStatus != ImportRowValidationStatus.Valid))
        {
            return Results.BadRequest(new { error = "NOT_FULLY_VALID", message = "All rows must be Valid to commit." });
        }

        // Commit is atomic (no partial commit)
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var row in rows)
            {
                var (candidate, errorCode, errorMessage) = TryParseFactRow(row.RawPayload);
                if (candidate == null)
                {
                    // safety: should not happen after validation
                    throw new InvalidOperationException($"Row {row.RowNumber} became invalid: {errorCode} {errorMessage}");
                }

                var fact = new Fact(
                    id: Guid.NewGuid(),
                    companyId: tenantId,
                    contractId: null,
                    externalReference: candidate.ExternalReference,
                    factType: candidate.FactType,
                    attributesJson: candidate.AttributesJson,
                    occurredAt: candidate.OccurredAt,
                    createdAt: now,
                    importBatchId: batchId,
                    payloadHash: Sha256Hex(row.RawPayload));

                db.Facts.Add(fact);
            }

            batch.MarkCommitted(DateTimeOffset.UtcNow);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            ImportMetrics.FactsCommitted.Add(rows.Count);

            await audit.WriteAsync(new AuditWriteRequest(
                CompanyId: tenantId,
                EntityType: "ImportBatch",
                EntityId: batchId.ToString(),
                Action: "Commit",
                CorrelationId: batchId.ToString(),
                DetailsJson: JsonSerializer.Serialize(new
                {
                    committedFacts = rows.Count,
                    status = batch.Status.ToString()
                })), ct);

            return Results.Ok(new { batchId, status = batch.Status, committedFacts = rows.Count });
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync(ct);
            batch.MarkFailed(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(new AuditWriteRequest(
                CompanyId: tenantId,
                EntityType: "ImportBatch",
                EntityId: batchId.ToString(),
                Action: "CommitFail",
                CorrelationId: batchId.ToString(),
                DetailsJson: JsonSerializer.Serialize(new
                {
                    error = "DB_UPDATE_ERROR",
                    message = ex.Message
                })), ct);

            return Results.Problem("Commit failed (db update error). Batch marked as Failed.", statusCode: 409);
        }
    }

    [Authorize(Policy = Permissions.ImportsUpload)]
    public static async Task<IResult> GetImportStatus(
        [FromRoute] Guid batchId,
        FixonDbContext db,
        CancellationToken ct)
    {
        var batch = await db.ImportBatches.AsNoTracking().SingleOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return Results.NotFound();

        var counts = await db.ImportRows.AsNoTracking()
            .Where(r => r.ImportBatchId == batchId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                total = g.Count(),
                valid = g.Count(x => x.ValidationStatus == ImportRowValidationStatus.Valid),
                invalid = g.Count(x => x.ValidationStatus == ImportRowValidationStatus.Invalid),
                duplicate = g.Count(x => x.ValidationStatus == ImportRowValidationStatus.Duplicate),
                pending = g.Count(x => x.ValidationStatus == ImportRowValidationStatus.Pending),
            })
            .SingleOrDefaultAsync(ct);

        return Results.Ok(new
        {
            batch.Id,
            batch.Source,
            batch.ExternalBatchId,
            batch.Status,
            batch.Checksum,
            batch.StartedAt,
            batch.FinishedAt,
            counts
        });
    }

    [Authorize(Policy = Permissions.ImportsUpload)]
    public static async Task<IResult> GetImportErrors(
        [FromRoute] Guid batchId,
        FixonDbContext db,
        CancellationToken ct)
    {
        var items = await db.ImportRows.AsNoTracking()
            .Where(r => r.ImportBatchId == batchId && r.ValidationStatus != ImportRowValidationStatus.Valid)
            .OrderBy(r => r.RowNumber)
            .Select(r => new
            {
                r.RowNumber,
                r.ValidationStatus,
                r.ErrorCode,
                r.ErrorMessage
            })
            .Take(500)
            .ToListAsync(ct);

        return Results.Ok(new { batchId, items });
    }

    private static (FactCandidate? candidate, string? errorCode, string? errorMessage) TryParseFactRow(string rawPayloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawPayloadJson);
            var root = doc.RootElement;

            var shipmentExternalId = GetRequiredString(root, "ShipmentExternalId");
            var factKey = GetRequiredString(root, "FactKey");
            var factValue = GetRequiredString(root, "FactValue");
            var recordedAtRaw = GetRequiredString(root, "RecordedAt");

            if (!DateTimeOffset.TryParse(recordedAtRaw, out var recordedAt))
            {
                return (null, "INVALID_RECORDED_AT", "RecordedAt must be a valid ISO datetime.");
            }

            var source = GetOptionalString(root, "Source");

            var externalReference = $"{shipmentExternalId}:{factKey}";
            var attributesJson = JsonSerializer.Serialize(new
            {
                shipmentExternalId,
                factKey,
                factValue,
                source
            });

            return (new FactCandidate(externalReference, factKey, attributesJson, recordedAt), null, null);
        }
        catch (JsonException ex)
        {
            return (null, "INVALID_JSON", ex.Message);
        }
        catch (Exception ex)
        {
            return (null, "SCHEMA_ERROR", ex.Message);
        }
    }

    private static string GetRequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"{name} is required.");
        }
        var s = p.GetString();
        if (string.IsNullOrWhiteSpace(s)) throw new FormatException($"{name} is required.");
        return s;
    }

    private static string? GetOptionalString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null) return null;
        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record FactCandidate(string ExternalReference, string FactType, string AttributesJson, DateTimeOffset OccurredAt);
}

public sealed record UploadFactsImportRequest(
    string Csv,
    string? ExternalBatchId,
    string? Delimiter);

