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
    /// <summary>
    /// POST /api/imports/facts
    /// Minimal CSV import (multipart/form-data, field: file).
    /// Expected columns: shipmentId,factType,eventTime,value
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

        var now = DateTimeOffset.UtcNow;
        var facts = new List<Fact>();

        // Skip header line.
        for (var i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            if (parts.Length < 4)
            {
                return Results.BadRequest(new { error = $"Invalid CSV row at line {i + 1}." });
            }

            var shipmentId = parts[0].Trim();
            var factType = parts[1].Trim();
            var eventTimeRaw = parts[2].Trim();
            var valueRaw = parts[3].Trim();

            if (string.IsNullOrWhiteSpace(shipmentId) || string.IsNullOrWhiteSpace(factType))
            {
                return Results.BadRequest(new { error = $"Invalid CSV row at line {i + 1}." });
            }

            if (!DateTimeOffset.TryParse(eventTimeRaw, out var eventTime))
            {
                return Results.BadRequest(new { error = $"Invalid eventTime at line {i + 1}." });
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
                valueNumber,
                valueText
            });

            facts.Add(new Fact(
                id: Guid.NewGuid(),
                companyId: userContext.TenantId,
                contractId: null,
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
