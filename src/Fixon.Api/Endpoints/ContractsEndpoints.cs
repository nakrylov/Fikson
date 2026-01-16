using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Audit;
using Fixon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Fixon.Api.Endpoints;

/// <summary>
/// Пример защищённого endpoint'а с authorization policies.
/// </summary>
public static class ContractsEndpoints
{
    /// <summary>
    /// GET /api/contracts
    /// Получение списка контрактов текущего tenant.
    /// Требует permission: contracts.read
    /// </summary>
    [Authorize(Policy = Permissions.ContractsRead)]
    public static async Task<IResult> GetContracts(
        FixonDbContext db,
        [FromServices] UserContext userContext,
        CancellationToken ct)
    {
        var items = await db.Contracts
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.CounterpartyId,
                x.Status,
                x.CurrentVersionId,
                x.CreatedAt
            })
            .ToListAsync(ct);

        return Results.Ok(new { tenantId = userContext.TenantId, items });
    }

    /// <summary>
    /// POST /api/contracts
    /// Создание нового контракта.
    /// Требует permission: contracts.manage
    /// </summary>
    [Authorize(Policy = Permissions.ContractsManage)]
    public static async Task<IResult> CreateContract(
        [FromBody] CreateContractRequest request,
        ClaimsPrincipal user,
        FixonDbContext db,
        IAuditWriter audit,
        CancellationToken ct)
    {
        // TenantId берётся из JWT claims (валидируется автоматически)
        var tenantIdClaim = user.FindFirst(FixonClaims.TenantId)?.Value;
        
        if (string.IsNullOrWhiteSpace(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return Results.BadRequest(new { error = "Tenant ID not found in token." });
        }

        var userIdClaim = user.FindFirst(FixonClaims.UserId)?.Value;
        _ = Guid.TryParse(userIdClaim, out var userId);

        var contractId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var contract = new Fixon.Domain.Contracts.Contract(
            id: contractId,
            companyId: tenantId,
            counterpartyId: request.CounterpartyId,
            name: request.Name,
            createdAt: now,
            isActive: true);

        // v1: создаём draft версию 1 сразу
        var versionId = Guid.NewGuid();
        var version = new Fixon.Domain.Contracts.ContractVersion(
            id: versionId,
            companyId: tenantId,
            contractId: contractId,
            versionNumber: 1,
            effectiveFrom: request.EffectiveFromUtc ?? now,
            effectiveTo: null,
            pdfFilePath: request.PdfFilePath,
            createdAt: now,
            createdByUserId: userId == Guid.Empty ? Guid.NewGuid() : userId,
            isActive: true);

        version.SetSnapshots(
            slaRulesSnapshotJson: request.SlaRulesSnapshotJson ?? "{}",
            penaltyRulesSnapshotJson: request.PenaltyRulesSnapshotJson ?? "{}");

        db.Contracts.Add(contract);
        db.ContractVersions.Add(version);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditWriteRequest(
            CompanyId: tenantId,
            EntityType: "Contract",
            EntityId: contractId.ToString(),
            Action: "Create",
            DetailsJson: JsonSerializer.Serialize(new
            {
                contractId,
                request.Name,
                request.CounterpartyId,
                initialVersionId = versionId,
                versionNumber = 1
            })), ct);

        return Results.Created($"/api/contracts/{contractId}", new
        {
            id = contractId,
            versionId,
            versionNumber = 1
        });
    }

    [Authorize(Policy = Permissions.ContractsRead)]
    public static async Task<IResult> GetContractHistory(
        [FromRoute] Guid contractId,
        FixonDbContext db,
        CancellationToken ct)
    {
        var contract = await db.Contracts.AsNoTracking()
            .Where(x => x.Id == contractId)
            .Select(x => new { x.Id, x.Name, x.Status, x.CurrentVersionId, x.CreatedAt })
            .SingleOrDefaultAsync(ct);

        if (contract is null) return Results.NotFound();

        var versions = await db.ContractVersions.AsNoTracking()
            .Where(v => v.ContractId == contractId)
            .OrderBy(v => v.VersionNumber)
            .Select(v => new
            {
                v.Id,
                v.VersionNumber,
                v.Status,
                v.EffectiveFrom,
                v.EffectiveTo,
                v.CreatedAt,
                v.SignedAt
            })
            .ToListAsync(ct);

        return Results.Ok(new { contract, versions });
    }

    [Authorize(Policy = Permissions.ContractsManage)]
    public static async Task<IResult> CreateContractVersion(
        [FromRoute] Guid contractId,
        [FromBody] CreateContractVersionRequest request,
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

        var userIdClaim = user.FindFirst(FixonClaims.UserId)?.Value;
        _ = Guid.TryParse(userIdClaim, out var userId);

        var contractExists = await db.Contracts.AnyAsync(c => c.Id == contractId, ct);
        if (!contractExists) return Results.NotFound();

        var lastVersion = await db.ContractVersions
            .Where(v => v.ContractId == contractId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        var nextNumber = lastVersion + 1;
        var now = DateTimeOffset.UtcNow;

        var versionId = Guid.NewGuid();
        var version = new Fixon.Domain.Contracts.ContractVersion(
            id: versionId,
            companyId: tenantId,
            contractId: contractId,
            versionNumber: nextNumber,
            effectiveFrom: request.EffectiveFromUtc,
            effectiveTo: request.EffectiveToUtc,
            pdfFilePath: request.PdfFilePath,
            createdAt: now,
            createdByUserId: userId == Guid.Empty ? Guid.NewGuid() : userId,
            isActive: true);

        version.SetSnapshots(
            slaRulesSnapshotJson: request.SlaRulesSnapshotJson ?? "{}",
            penaltyRulesSnapshotJson: request.PenaltyRulesSnapshotJson ?? "{}");

        db.ContractVersions.Add(version);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditWriteRequest(
            CompanyId: tenantId,
            EntityType: "ContractVersion",
            EntityId: versionId.ToString(),
            Action: "Create",
            DetailsJson: JsonSerializer.Serialize(new
            {
                contractId,
                versionId,
                versionNumber = nextNumber
            })), ct);

        return Results.Created($"/api/contracts/{contractId}/versions/{versionId}", new { id = versionId, versionNumber = nextNumber });
    }

    [Authorize(Policy = Permissions.ContractsManage)]
    public static async Task<IResult> SignContractVersion(
        [FromRoute] Guid contractId,
        [FromRoute] Guid versionId,
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

        var version = await db.ContractVersions
            .Where(v => v.ContractId == contractId && v.Id == versionId)
            .SingleOrDefaultAsync(ct);

        if (version is null) return Results.NotFound();

        version.Sign(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditWriteRequest(
            CompanyId: tenantId,
            EntityType: "ContractVersion",
            EntityId: versionId.ToString(),
            Action: "Sign",
            DetailsJson: JsonSerializer.Serialize(new { contractId, versionId })), ct);

        return Results.Ok(new { versionId, status = version.Status, version.SignedAt });
    }

    [Authorize(Policy = Permissions.ContractsManage)]
    public static async Task<IResult> ActivateContractVersion(
        [FromRoute] Guid contractId,
        [FromRoute] Guid versionId,
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

        var contract = await db.Contracts
            .Include(c => c.Versions)
            .SingleOrDefaultAsync(c => c.Id == contractId, ct);

        if (contract is null) return Results.NotFound();

        var version = contract.Versions.SingleOrDefault(v => v.Id == versionId);
        if (version is null) return Results.NotFound();
        if (version.Status != Fixon.Domain.Contracts.ContractVersionStatus.Signed)
        {
            return Results.BadRequest(new { error = "Only Signed version can be activated." });
        }

        // archive previous current version (if any)
        if (contract.CurrentVersionId.HasValue && contract.CurrentVersionId.Value != versionId)
        {
            var prev = contract.Versions.SingleOrDefault(v => v.Id == contract.CurrentVersionId.Value);
            prev?.Archive();
        }

        contract.ActivateVersion(versionId);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditWriteRequest(
            CompanyId: tenantId,
            EntityType: "Contract",
            EntityId: contractId.ToString(),
            Action: "ActivateVersion",
            DetailsJson: JsonSerializer.Serialize(new { contractId, versionId })), ct);

        return Results.Ok(new { contractId, currentVersionId = contract.CurrentVersionId, contract.Status });
    }

    [Authorize(Policy = Permissions.ContractsManage)]
    public static async Task<IResult> TerminateContract(
        [FromRoute] Guid contractId,
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

        var contract = await db.Contracts.SingleOrDefaultAsync(c => c.Id == contractId, ct);
        if (contract is null) return Results.NotFound();

        contract.Terminate();
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditWriteRequest(
            CompanyId: tenantId,
            EntityType: "Contract",
            EntityId: contractId.ToString(),
            Action: "Terminate",
            DetailsJson: JsonSerializer.Serialize(new { contractId })), ct);

        return Results.Ok(new { contractId, contract.Status });
    }
}

public sealed class CreateContractRequest
{
    public string Name { get; set; } = null!;
    public Guid CounterpartyId { get; set; }
    public DateTimeOffset? EffectiveFromUtc { get; set; }
    public string? PdfFilePath { get; set; }
    public string? SlaRulesSnapshotJson { get; set; }
    public string? PenaltyRulesSnapshotJson { get; set; }
}

public sealed class CreateContractVersionRequest
{
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
    public string? PdfFilePath { get; set; }
    public string? SlaRulesSnapshotJson { get; set; }
    public string? PenaltyRulesSnapshotJson { get; set; }
}

