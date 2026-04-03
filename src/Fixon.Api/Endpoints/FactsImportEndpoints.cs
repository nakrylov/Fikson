using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Fixon.Api.Imports;
using Fixon.Api.Security;
using Fixon.Api.Services;
using Fixon.Domain.Facts;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fixon.Api.Endpoints;

public static class FactsImportEndpoints
{
    private static readonly string[] EventRequiredHeaders = ["shipmentId", "factType", "eventTime", "value"];
    private static readonly string[] TabularRequiredHeaders = ["shipmentId", "counterpartyCode"];
    private static readonly string[] BaseTemplateColumns = ["shipmentId", "counterpartyCode", "cargoType"];
    private static readonly IReadOnlyDictionary<string, string[]> MetricToTemplateColumns =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["DELIVERY_DELAY"] = [ImportMappingConfig.PlannedDeliveryTimeColumn, ImportMappingConfig.ActualDeliveryTimeColumn],
            ["TEMPERATURE"] = [ImportMappingConfig.TemperatureColumn],
            ["DOCUMENT_MISSING"] = [ImportMappingConfig.DocumentsMissingColumn],
            ["MISSING_DOCS"] = [ImportMappingConfig.DocumentsMissingColumn]
        };

    [Authorize(Policy = Permissions.ImportsUpload)]
    [RequireTenant]
    public static async Task<IResult> DownloadTemplate(
        FixonDbContext db,
        CancellationToken ct)
    {
        var columns = await BuildTemplateColumnsAsync(db, ct);
        var csv = BuildTemplateCsv(columns);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
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
        ILoggerFactory loggerFactory,
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

        var delimiter = lines[0].Contains(';') ? ';' : ',';
        var headers = lines[0].Split(delimiter).Select(x => x.Trim()).ToArray();
        var headerIndex = headers
            .Select((name, index) => new { name, index })
            .GroupBy(x => x.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().index, StringComparer.OrdinalIgnoreCase);

        var isTabularFormat = ImportMappingConfig.TabularHintColumns.Any(h => headerIndex.ContainsKey(h));
        var missingHeaders = (isTabularFormat ? TabularRequiredHeaders : EventRequiredHeaders)
            .Where(h => !headerIndex.ContainsKey(h))
            .ToArray();
        if (missingHeaders.Length > 0)
        {
            return Results.BadRequest(new { error = $"Invalid CSV header. Missing columns: {string.Join(", ", missingHeaders)}." });
        }

        var logger = loggerFactory.CreateLogger("FactsImportEndpoints");
        var now = DateTimeOffset.UtcNow;
        var facts = new List<Fact>();
        var skippedRows = 0;
        var importedRows = 0;
        var generatedEvents = 0;

        var counterparties = await db.Counterparties.ToListAsync(ct);
        var counterpartiesByCode = counterparties
            .Where(x => !string.IsNullOrWhiteSpace(x.ExternalCode))
            .GroupBy(x => x.ExternalCode!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var counterpartiesByName = counterparties
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var activeContracts = await db.Contracts
            .AsNoTracking()
            .Where(x =>
                x.Status == Fixon.Domain.Contracts.ContractStatus.Active
                && x.CurrentVersionId != null
                && x.CounterpartyId != null)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.CounterpartyId })
            .ToListAsync(ct);
        var contractByCounterpartyId = activeContracts
            .GroupBy(x => x.CounterpartyId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Id);

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
            var counterpartyCode = headerIndex.ContainsKey("counterpartyCode")
                ? GetCell("counterpartyCode")
                : string.Empty;
            if (string.IsNullOrWhiteSpace(shipmentId))
            {
                skippedRows++;
                continue;
            }

            var cargoType = headerIndex.ContainsKey("cargoType") ? GetCell("cargoType") : null;

            if (isTabularFormat)
            {
                if (string.IsNullOrWhiteSpace(counterpartyCode))
                {
                    skippedRows++;
                    continue;
                }

                var tabularCounterparty = await ResolveOrCreateCounterparty(
                    counterpartyCode,
                    userContext.TenantId,
                    db,
                    counterpartiesByCode,
                    counterpartiesByName,
                    ct);

                Guid? tabularContractId = null;
                if (contractByCounterpartyId.TryGetValue(tabularCounterparty.Id, out var linkedContractId))
                {
                    tabularContractId = linkedContractId;
                }

                importedRows++;
                var plannedDeliveryRaw = headerIndex.ContainsKey(ImportMappingConfig.PlannedDeliveryTimeColumn)
                    ? GetCell(ImportMappingConfig.PlannedDeliveryTimeColumn)
                    : string.Empty;
                var actualDeliveryRaw = headerIndex.ContainsKey(ImportMappingConfig.ActualDeliveryTimeColumn)
                    ? GetCell(ImportMappingConfig.ActualDeliveryTimeColumn)
                    : string.Empty;
                var temperatureRaw = headerIndex.ContainsKey(ImportMappingConfig.TemperatureColumn)
                    ? GetCell(ImportMappingConfig.TemperatureColumn)
                    : string.Empty;
                var documentsMissingRaw = headerIndex.ContainsKey(ImportMappingConfig.DocumentsMissingColumn)
                    ? GetCell(ImportMappingConfig.DocumentsMissingColumn)
                    : string.Empty;

                DateTimeOffset? plannedDelivery = TryParseDate(plannedDeliveryRaw);
                DateTimeOffset? actualDelivery = TryParseDate(actualDeliveryRaw);

                if (plannedDelivery.HasValue)
                {
                    facts.Add(CreateFact(
                        userContext.TenantId,
                        tabularContractId,
                        shipmentId,
                        externalReference: null,
                        ImportMappingConfig.TabularColumnToEventType[ImportMappingConfig.PlannedDeliveryTimeColumn],
                        plannedDelivery.Value,
                        counterpartyCode,
                        tabularCounterparty.Id,
                        ImportMappingConfig.TabularColumnToEventType[ImportMappingConfig.PlannedDeliveryTimeColumn],
                        cargoType,
                        null,
                        "tabular_import",
                        now));
                    generatedEvents++;
                }

                if (actualDelivery.HasValue)
                {
                    facts.Add(CreateFact(
                        userContext.TenantId,
                        tabularContractId,
                        shipmentId,
                        externalReference: null,
                        ImportMappingConfig.TabularColumnToEventType[ImportMappingConfig.ActualDeliveryTimeColumn],
                        actualDelivery.Value,
                        counterpartyCode,
                        tabularCounterparty.Id,
                        ImportMappingConfig.TabularColumnToEventType[ImportMappingConfig.ActualDeliveryTimeColumn],
                        cargoType,
                        null,
                        "tabular_import",
                        now));
                    generatedEvents++;
                }

                if (!string.IsNullOrWhiteSpace(temperatureRaw) &&
                    double.TryParse(temperatureRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var temperatureValue))
                {
                    facts.Add(CreateFact(
                        userContext.TenantId,
                        tabularContractId,
                        shipmentId,
                        externalReference: null,
                        "TEMPERATURE",
                        actualDelivery ?? plannedDelivery ?? now,
                        counterpartyCode,
                        tabularCounterparty.Id,
                        ImportMappingConfig.TabularColumnToEventType[ImportMappingConfig.TemperatureColumn],
                        cargoType,
                        temperatureValue,
                        "tabular_import",
                        now));
                    generatedEvents++;
                }

                if (TryParseBoolean(documentsMissingRaw))
                {
                    facts.Add(CreateFact(
                        userContext.TenantId,
                        tabularContractId,
                        shipmentId,
                        externalReference: null,
                        ImportMappingConfig.TabularColumnToEventType[ImportMappingConfig.DocumentsMissingColumn],
                        actualDelivery ?? now,
                        counterpartyCode,
                        tabularCounterparty.Id,
                        ImportMappingConfig.TabularColumnToEventType[ImportMappingConfig.DocumentsMissingColumn],
                        cargoType,
                        null,
                        "tabular_import",
                        now));
                    generatedEvents++;
                }

                continue;
            }

            var factType = GetCell("factType");
            var eventTimeRaw = GetCell("eventTime");
            var valueRaw = GetCell("value");
            var eventType = headerIndex.ContainsKey("eventType") ? GetCell("eventType") : factType;
            Fixon.Domain.Contracts.Counterparty? counterparty = null;
            Guid? contractId = null;
            if (!string.IsNullOrWhiteSpace(counterpartyCode))
            {
                counterparty = await ResolveOrCreateCounterparty(
                    counterpartyCode,
                    userContext.TenantId,
                    db,
                    counterpartiesByCode,
                    counterpartiesByName,
                    ct);
                if (contractByCounterpartyId.TryGetValue(counterparty.Id, out var linkedContractId))
                {
                    contractId = linkedContractId;
                }
            }

            if (string.IsNullOrWhiteSpace(factType))
            {
                skippedRows++;
                continue;
            }

            if (!DateTimeOffset.TryParse(eventTimeRaw, out var eventTime))
            {
                skippedRows++;
                continue;
            }

            double? valueNumber = null;
            if (!string.IsNullOrWhiteSpace(valueRaw) &&
                double.TryParse(valueRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                valueNumber = parsed;
            }

            facts.Add(CreateFact(
                userContext.TenantId,
                contractId,
                shipmentId,
                externalReference: shipmentId,
                factType,
                eventTime,
                counterpartyCode,
                counterparty?.Id,
                string.IsNullOrWhiteSpace(eventType) ? factType : eventType,
                cargoType,
                valueNumber,
                "event_import",
                now));
            importedRows++;
            generatedEvents++;
        }

        var userIdClaim = user.FindFirst(FixonClaims.UserId)?.Value;
        var createdByUserId = Guid.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : Guid.Empty;
        if (createdByUserId == Guid.Empty)
        {
            return Results.BadRequest(new { error = "User ID not found in token." });
        }

        var duplicateFactsSkipped = await DeduplicateFactsAsync(userContext.TenantId, db, facts, ct);
        if (duplicateFactsSkipped > 0)
        {
            skippedRows += duplicateFactsSkipped;
            generatedEvents = Math.Max(generatedEvents - duplicateFactsSkipped, 0);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        int claimsGenerated;
        try
        {
            if (facts.Count > 0)
            {
                db.Facts.AddRange(facts);
                await db.SaveChangesAsync(ct);
            }

            claimsGenerated = await slaEvaluationService.RunEvaluationForTenant(userContext.TenantId, createdByUserId, ct);

            db.FactImports.Add(new FactImport(
                id: Guid.NewGuid(),
                companyId: userContext.TenantId,
                fileName: file.FileName,
                rowsImported: importedRows,
                claimsGenerated: claimsGenerated,
                createdAt: now,
                createdByUserId: createdByUserId));

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Import failed while saving facts.");
            await tx.RollbackAsync(ct);
            return Results.BadRequest(new { error = "Import failed due to duplicate or invalid rows in CSV." });
        }

        logger.LogInformation(
            "Fact import processed. processedRows={ProcessedRows}, importedRows={ImportedRows}, generatedEvents={GeneratedEvents}, skippedRows={SkippedRows}, duplicateFactsSkipped={DuplicateFactsSkipped}, format={Format}",
            Math.Max(lines.Length - 1, 0),
            importedRows,
            generatedEvents,
            skippedRows,
            duplicateFactsSkipped,
            isTabularFormat ? "tabular" : "event");

        return Results.Ok(new
        {
            importedRows,
            generatedEvents,
            skippedRows,
            imported = generatedEvents,
            claimsGenerated
        });
    }

    private static async Task<Fixon.Domain.Contracts.Counterparty> ResolveOrCreateCounterparty(
        string counterpartyCode,
        Guid tenantId,
        FixonDbContext db,
        Dictionary<string, Fixon.Domain.Contracts.Counterparty> counterpartiesByCode,
        Dictionary<string, Fixon.Domain.Contracts.Counterparty> counterpartiesByName,
        CancellationToken ct)
    {
        var key = counterpartyCode.Trim();
        if (counterpartiesByCode.TryGetValue(key, out var byCode))
        {
            return byCode;
        }

        if (counterpartiesByName.TryGetValue(key, out var byName))
        {
            return byName;
        }

        var created = new Fixon.Domain.Contracts.Counterparty(
            id: Guid.NewGuid(),
            companyId: tenantId,
            name: key,
            externalCode: key,
            isActive: true);

        db.Counterparties.Add(created);
        await db.SaveChangesAsync(ct);
        counterpartiesByCode[key] = created;
        counterpartiesByName[key] = created;
        return created;
    }

    private static async Task<List<string>> BuildTemplateColumnsAsync(FixonDbContext db, CancellationToken ct)
    {
        var columns = new List<string>(BaseTemplateColumns);
        var includedColumns = new HashSet<string>(BaseTemplateColumns, StringComparer.OrdinalIgnoreCase);
        var activeRules = await (
            from rule in db.SlaRules.AsNoTracking()
            join contract in db.Contracts.AsNoTracking() on rule.ContractId equals contract.Id
            where rule.IsActive
                  && contract.Status == Fixon.Domain.Contracts.ContractStatus.Active
                  && contract.CurrentVersionId != null
            select new { rule.Id, rule.Name, rule.ConditionType }
        ).ToListAsync(ct);

        if (activeRules.Count == 0)
        {
            return columns;
        }

        var activeRuleVersions = await db.SlaRuleVersions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.SlaRuleId, x.VersionNumber, x.ConditionJson })
            .ToListAsync(ct);
        var latestConditionByRule = activeRuleVersions
            .GroupBy(x => x.SlaRuleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.VersionNumber).First().ConditionJson);

        foreach (var rule in activeRules)
        {
            var metric = rule.Name;
            if (latestConditionByRule.TryGetValue(rule.Id, out var conditionJson))
            {
                metric = TryReadString(conditionJson, "metric") ?? metric;
            }

            if (!MetricToTemplateColumns.TryGetValue(metric.Trim().ToUpperInvariant(), out var requiredColumns))
            {
                continue;
            }

            foreach (var column in requiredColumns)
            {
                if (includedColumns.Add(column))
                {
                    columns.Add(column);
                }
            }
        }

        return columns;
    }

    private static string BuildTemplateCsv(IReadOnlyList<string> columns)
    {
        var sampleByColumn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["shipmentId"] = "SHP-001",
            ["counterpartyCode"] = "CONTOSO",
            ["cargoType"] = "ICE_CREAM",
            [ImportMappingConfig.PlannedDeliveryTimeColumn] = "2026-01-01T10:00:00Z",
            [ImportMappingConfig.ActualDeliveryTimeColumn] = "2026-01-01T10:45:00Z",
            [ImportMappingConfig.TemperatureColumn] = "-5",
            [ImportMappingConfig.DocumentsMissingColumn] = "false"
        };

        var header = string.Join(';', columns);
        var row = string.Join(';', columns.Select(column =>
            sampleByColumn.TryGetValue(column, out var value) ? value : string.Empty));

        return $"{header}\n{row}\n";
    }

    private static async Task<int> DeduplicateFactsAsync(
        Guid companyId,
        FixonDbContext db,
        List<Fact> facts,
        CancellationToken ct)
    {
        if (facts.Count == 0)
        {
            return 0;
        }

        var beforeCount = facts.Count;

        // First, remove duplicates inside the same CSV payload by unique key.
        var inFileSeen = new HashSet<(string ExternalReference, DateTimeOffset OccurredAt)>();
        facts.RemoveAll(f =>
        {
            if (string.IsNullOrWhiteSpace(f.ExternalReference))
            {
                return false;
            }

            var key = (f.ExternalReference!.Trim(), f.OccurredAt);
            if (inFileSeen.Contains(key))
            {
                return true;
            }

            inFileSeen.Add(key);
            return false;
        });

        var withExternalReference = facts
            .Where(f => !string.IsNullOrWhiteSpace(f.ExternalReference))
            .Select(f => (ExternalReference: f.ExternalReference!.Trim(), f.OccurredAt))
            .Distinct()
            .ToList();
        if (withExternalReference.Count == 0)
        {
            return beforeCount - facts.Count;
        }

        var externalReferences = withExternalReference
            .Select(x => x.ExternalReference)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var minOccurredAt = withExternalReference.Min(x => x.OccurredAt);
        var maxOccurredAt = withExternalReference.Max(x => x.OccurredAt);

        var existingKeys = await db.Facts
            .AsNoTracking()
            .Where(f =>
                f.CompanyId == companyId
                && f.ExternalReference != null
                && externalReferences.Contains(f.ExternalReference)
                && f.OccurredAt >= minOccurredAt
                && f.OccurredAt <= maxOccurredAt)
            .Select(f => new { f.ExternalReference, f.OccurredAt })
            .ToListAsync(ct);

        var existingSet = existingKeys
            .Where(x => !string.IsNullOrWhiteSpace(x.ExternalReference))
            .Select(x => (x.ExternalReference!, x.OccurredAt))
            .ToHashSet();

        facts.RemoveAll(f =>
            !string.IsNullOrWhiteSpace(f.ExternalReference)
            && existingSet.Contains((f.ExternalReference!.Trim(), f.OccurredAt)));

        return beforeCount - facts.Count;
    }

    private static DateTimeOffset? TryParseDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static bool TryParseBoolean(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        return normalized is "true" or "1" or "yes" or "y";
    }

    private static string? TryReadString(string json, string propertyName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(propertyName, out var p) && p.ValueKind == JsonValueKind.String)
            {
                return p.GetString();
            }
        }
        catch (JsonException)
        {
            // Ignore malformed JSON and fallback to defaults.
        }

        return null;
    }

    private static Fact CreateFact(
        Guid companyId,
        Guid? contractId,
        string shipmentId,
        string? externalReference,
        string factType,
        DateTimeOffset eventTime,
        string counterpartyCode,
        Guid? counterpartyId,
        string eventType,
        string? cargoType,
        double? valueNumber,
        string source,
        DateTimeOffset createdAt)
    {
        var attributesJson = JsonSerializer.Serialize(new
        {
            shipmentId,
            counterpartyCode,
            counterpartyId,
            eventType,
            value = valueNumber,
            valueNumber,
            cargoType,
            source
        });

        return new Fact(
            id: Guid.NewGuid(),
            companyId: companyId,
            contractId: contractId,
            externalReference: externalReference,
            factType: factType,
            attributesJson: attributesJson,
            occurredAt: eventTime,
            createdAt: createdAt);
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
