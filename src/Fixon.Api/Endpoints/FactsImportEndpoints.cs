using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Fixon.Api.Security;
using Fixon.Api.Services;
using Fixon.Domain.Facts;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixon.Api.Endpoints;

public static class FactsImportEndpoints
{
    private static readonly string[] RequiredHeaders = ["shipmentId", "factType", "eventTime", "value"];

    private const string FactImportTemplateCsv =
        "shipmentId;factType;eventType;cargoType;eventTime;value;counterpartyCode\n" +
        "SHP-001;DELIVERY_DELAY;DELIVERY_DELAY;ICE_CREAM;2026-01-01T10:00:00Z;45;CONTOSO\n" +
        "SHP-002;DOCUMENT_MISSING;DOCUMENT_MISSING;FROZEN_FISH;2026-01-01T11:00:00Z;1;NORTHWIND\n";

    [Authorize(Policy = Permissions.ImportsUpload)]
    [RequireTenant]
    public static IResult DownloadTemplate()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(FactImportTemplateCsv);
        return Results.File(
            fileContents: bytes,
            contentType: "text/csv",
            fileDownloadName: "fact_import_template.csv");
    }

    /// <summary>
    /// POST /api/imports/facts
    /// Minimal CSV import (multipart/form-data, field: file).
    /// Expected columns: shipmentId,factType,eventTime,value (+ optional counterpartyCode,cargoType)
    /// </summary>
    [Authorize(Policy = Permissions.ImportsUpload)]
    [RequireTenant]
    public static async Task<IResult> ImportFacts(
        [FromForm] IFormFile file,
        ClaimsPrincipal user,
        UserContext userContext,
        SlaEvaluationService slaEvaluationService,
        FixonDbContext db,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "File is required." });
        }

        string csv;
        await using (var stream = file.OpenReadStream())
        using (var reader = new StreamReader(stream))
        {
            csv = await reader.ReadToEndAsync(ct);
        }

        var lines = csv
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return Results.BadRequest(new { error = "CSV is empty." });
        }

        var delimiter = lines.Length > 0 && lines[0].Contains(';') ? ';' : ',';
        var headers = lines[0].Split(delimiter).Select(x => x.Trim()).ToArray();
        var headerIndex = headers
            .Select((name, index) => new { name, index })
            .GroupBy(x => x.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().index, StringComparer.OrdinalIgnoreCase);
        var missingHeaders = RequiredHeaders.Where(h => !headerIndex.ContainsKey(h)).ToArray();
        if (missingHeaders.Length > 0)
        {
            return Results.BadRequest(new { error = $"Invalid CSV header. Missing columns: {string.Join(", ", missingHeaders)}." });
        }

        var counterparties = await db.Counterparties
            .AsNoTracking()
            .Select(x => new { x.Id, x.Name, x.ExternalCode })
            .ToListAsync(ct);

        var counterpartiesByName = counterparties
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var counterpartiesByCode = counterparties
            .Where(x => !string.IsNullOrWhiteSpace(x.ExternalCode))
            .GroupBy(x => x.ExternalCode!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var activeContracts = await db.Contracts
            .AsNoTracking()
            .Where(x => x.Status == Fixon.Domain.Contracts.ContractStatus.Active && x.CurrentVersionId != null)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.CounterpartyId })
            .ToListAsync(ct);

        var contractByCounterpartyId = activeContracts
            .GroupBy(x => x.CounterpartyId)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var now = DateTimeOffset.UtcNow;
        var facts = new List<Fact>();

        // Skip header line.
        for (var i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(delimiter);
            string GetCell(string headerName)
            {
                var index = headerIndex[headerName];
                return index < parts.Length ? parts[index].Trim() : string.Empty;
            }

            var shipmentId = GetCell("shipmentId");
            var factType = GetCell("factType");
            var eventTimeRaw = GetCell("eventTime");
            var valueRaw = GetCell("value");
            var hasCounterpartyCodeColumn = headerIndex.ContainsKey("counterpartyCode");
            var counterpartyCodeRaw = hasCounterpartyCodeColumn ? GetCell("counterpartyCode") : string.Empty;
            var cargoType = headerIndex.ContainsKey("cargoType") ? GetCell("cargoType") : null;
            var eventType = headerIndex.ContainsKey("eventType") ? GetCell("eventType") : factType;

            if (string.IsNullOrWhiteSpace(shipmentId) || string.IsNullOrWhiteSpace(factType))
            {
                return Results.BadRequest(new { error = $"Invalid CSV row at line {i + 1}." });
            }

            if (!DateTimeOffset.TryParse(eventTimeRaw, out var eventTime))
            {
                return Results.BadRequest(new { error = $"Invalid eventTime at line {i + 1}." });
            }

            var counterpartyCode = counterpartyCodeRaw.Trim();
            Guid? counterpartyId = null;
            Guid? contractId = null;
            if (!string.IsNullOrWhiteSpace(counterpartyCode))
            {
                if (counterpartiesByCode.TryGetValue(counterpartyCode, out var byCodeId))
                {
                    counterpartyId = byCodeId;
                }
                else if (counterpartiesByName.TryGetValue(counterpartyCode, out var byNameId))
                {
                    counterpartyId = byNameId;
                }

                if (!counterpartyId.HasValue)
                {
                    return Results.BadRequest(new { error = $"Counterparty '{counterpartyCode}' not found at line {i + 1}." });
                }

                if (!contractByCounterpartyId.TryGetValue(counterpartyId.Value, out var activeContractId))
                {
                    return Results.BadRequest(new { error = $"No active contract found for counterparty '{counterpartyCode}' at line {i + 1}." });
                }

                contractId = activeContractId;
            }

            double? valueNumber = null;
            string? valueText = null;
            if (!string.IsNullOrWhiteSpace(valueRaw))
            {
                if (double.TryParse(valueRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    valueNumber = parsed;
                }
                else
                {
                    valueText = valueRaw;
                }
            }

            var attributesJson = JsonSerializer.Serialize(new
            {
                shipmentId,
                counterpartyCode,
                counterpartyId,
                eventType,
                cargoType,
                valueNumber,
                valueText
            });

            facts.Add(new Fact(
                id: Guid.NewGuid(),
                companyId: userContext.TenantId,
                contractId: contractId,
                externalReference: shipmentId,
                factType: factType,
                attributesJson: attributesJson,
                occurredAt: eventTime,
                createdAt: now));
        }

        var userIdClaim = user.FindFirst(FixonClaims.UserId)?.Value;
        var createdByUserId = Guid.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : Guid.Empty;
        if (createdByUserId == Guid.Empty)
        {
            return Results.BadRequest(new { error = "User ID not found in token." });
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (facts.Count > 0)
        {
            db.Facts.AddRange(facts);
            await db.SaveChangesAsync(ct);
        }

        var claimsGenerated = await slaEvaluationService.RunEvaluationForTenant(userContext.TenantId, createdByUserId, ct);

        db.FactImports.Add(new FactImport(
            id: Guid.NewGuid(),
            companyId: userContext.TenantId,
            fileName: file.FileName,
            rowsImported: facts.Count,
            claimsGenerated: claimsGenerated,
            createdAt: now,
            createdByUserId: createdByUserId));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.Ok(new { imported = facts.Count, claimsGenerated });
    }

    [Authorize(Policy = Permissions.ImportsUpload)]
    [RequireTenant]
    public static async Task<IResult> GetImportHistory(
        FixonDbContext db,
        CancellationToken ct)
    {
        var items = await db.FactImports
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                id = x.Id,
                fileName = x.FileName,
                rowsImported = x.RowsImported,
                claimsGenerated = x.ClaimsGenerated,
                createdAt = x.CreatedAt
            })
            .ToListAsync(ct);

        return Results.Ok(items);
    }
}
