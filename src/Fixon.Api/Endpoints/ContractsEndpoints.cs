using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Audit;
using Fixon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
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

    [Authorize(Policy = Permissions.ContractsRead)]
    public static async Task<IResult> GetContract(
        [FromRoute] Guid contractId,
        FixonDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var contract = await db.Contracts
            .AsNoTracking()
            .Where(x => x.Id == contractId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.CounterpartyId,
                x.Status,
                x.CurrentVersionId,
                x.CreatedAt,
                xmin = EF.Property<uint>(x, "xmin")
            })
            .SingleOrDefaultAsync(ct);

        if (contract is null) return Results.NotFound();

        // RFC: ETag value must be quoted.
        httpContext.Response.Headers.ETag = $"\"{contract.xmin}\"";

        return Results.Ok(new
        {
            contract.Id,
            contract.Name,
            contract.CounterpartyId,
            contract.Status,
            contract.CurrentVersionId,
            contract.CreatedAt
        });
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
        HttpContext httpContext,
        IWebHostEnvironment env,
        CancellationToken ct)
    {
        // Idempotency-Key is required for contract creation (prevents accidental duplicates).
        if (!httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyValues) ||
            string.IsNullOrWhiteSpace(idempotencyKeyValues.ToString()))
        {
            return Results.BadRequest(new { error = "Idempotency-Key header is required." });
        }

        var idempotencyKey = idempotencyKeyValues.ToString().Trim();
        var endpoint = httpContext.Request.Path.Value ?? "/api/contracts";

        // TenantId берётся из JWT claims (валидируется автоматически)
        var tenantIdClaim = user.FindFirst(FixonClaims.TenantId)?.Value;
        
        if (string.IsNullOrWhiteSpace(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return Results.BadRequest(new { error = "Tenant ID not found in token." });
        }

        // Basic request validation (avoid creating invalid data, return 400 with problem details)
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["Name"] = new[] { "Name is required." };
        }

        if (request.CounterpartyId == Guid.Empty)
        {
            errors["CounterpartyId"] = new[] { "CounterpartyId is required." };
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // Idempotency implementation (race-safe):
        // - Insert a placeholder row first (unique key acts as a mutex).
        // - Lock the row with SELECT ... FOR UPDATE.
        // - If response already stored -> return it.
        // - Otherwise create the contract, store response into the row, commit.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            // Try to create placeholder row (no-op if already exists).
            // We use raw SQL to avoid tracking issues and to rely on ON CONFLICT semantics.
            await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO idempotency_requests (""TenantId"", ""Key"", ""Endpoint"", ""ResponseStatusCode"", ""ResponseBody"", ""CreatedAt"")
VALUES ({tenantId}, {idempotencyKey}, {endpoint}, {0}, '{{}}'::jsonb, {DateTimeOffset.UtcNow})
ON CONFLICT (""TenantId"", ""Key"", ""Endpoint"") DO NOTHING;", ct);

            // Debug-only: simulate a controlled failure after placeholder insert,
            // to verify idempotency recovery on retry. Available only in Development/Testing.
            if ((env.IsDevelopment() || env.IsEnvironment("Testing")) &&
                httpContext.Request.Headers.TryGetValue("X-Debug-Fail-After-Idempotency", out var debugFailValues) &&
                string.Equals(debugFailValues.ToString(), "true", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("DEBUG_FAIL_AFTER_IDEMPOTENCY");
            }

            // Lock the row so concurrent requests with the same key will wait until we commit.
            var locked = await db.IdempotencyRequests
                .FromSqlInterpolated($@"
SELECT ""TenantId"", ""Key"", ""Endpoint"", ""ResponseStatusCode"", ""ResponseBody"", ""CreatedAt""
FROM idempotency_requests
WHERE ""TenantId"" = {tenantId} AND ""Key"" = {idempotencyKey} AND ""Endpoint"" = {endpoint}
FOR UPDATE")
                .SingleAsync(ct);

            // If already completed by another request, return stored response.
            if (locked.ResponseStatusCode != 0)
            {
                await tx.CommitAsync(ct);
                return Results.Text(locked.ResponseBody, contentType: "application/json", statusCode: locked.ResponseStatusCode);
            }

            // Prevent duplicates (business rule): contract Name must be unique within tenant.
            // NOTE: enforced at API level (no DB unique constraint).
            if (await db.Contracts.AsNoTracking().AnyAsync(x => x.Name == request.Name, ct))
            {
                return Results.Conflict(new
                {
                    error = "DUPLICATE_CONTRACT",
                    message = "Contract with the same name already exists."
                });
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

            var responseBody = JsonSerializer.Serialize(new
            {
                id = contractId,
                versionId,
                versionNumber = 1
            });

            // Store idempotent response into the locked row and commit transaction.
            db.Entry(locked).Property(nameof(IdempotencyRequest.ResponseStatusCode)).CurrentValue = StatusCodes.Status201Created;
            db.Entry(locked).Property(nameof(IdempotencyRequest.ResponseBody)).CurrentValue = responseBody;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Results.Text(responseBody, contentType: "application/json", statusCode: StatusCodes.Status201Created);
        }
        catch (InvalidOperationException ex) when (ex.Message == "DEBUG_FAIL_AFTER_IDEMPOTENCY")
        {
            await tx.RollbackAsync(ct);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [Authorize(Policy = Permissions.ContractsManage)]
    public static async Task<IResult> UpdateContract(
        [FromRoute] Guid contractId,
        [FromBody] UpdateContractRequest request,
        FixonDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Name"] = new[] { "Name is required." }
            });
        }

        if (!httpContext.Request.Headers.TryGetValue("If-Match", out var ifMatchValues) ||
            string.IsNullOrWhiteSpace(ifMatchValues.ToString()))
        {
            return Results.BadRequest(new { error = "If-Match header is required." });
        }

        if (!TryParseEtagVersion(ifMatchValues.ToString(), out var expectedXmin))
        {
            return Results.BadRequest(new { error = "Invalid If-Match ETag format." });
        }

        var contract = await db.Contracts.SingleOrDefaultAsync(x => x.Id == contractId, ct);
        if (contract is null) return Results.NotFound();

        contract.Rename(request.Name);

        // Set original xmin (shadow property) for optimistic concurrency check.
        db.Entry(contract).Property("xmin").OriginalValue = expectedXmin;

        try
        {
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.StatusCode(StatusCodes.Status412PreconditionFailed);
        }
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

    [Authorize(Policy = Permissions.ContractsManage)]
    public static async Task<IResult> DeleteContract(
        [FromRoute] Guid contractId,
        ClaimsPrincipal user,
        FixonDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var tenantIdClaim = user.FindFirst(FixonClaims.TenantId)?.Value;
        if (string.IsNullOrWhiteSpace(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return Results.BadRequest(new { error = "Tenant ID not found in token." });
        }

        // Optional optimistic concurrency for DELETE: if If-Match is provided, enforce it.
        uint? expectedXmin = null;
        var hasIfMatch =
            httpContext.Request.Headers.TryGetValue("If-Match", out var ifMatchValues)
            && !string.IsNullOrWhiteSpace(ifMatchValues.ToString());

        if (hasIfMatch)
        {
            if (!TryParseEtagVersion(ifMatchValues.ToString(), out var parsed))
            {
                return Results.BadRequest(new { error = "Invalid If-Match ETag format." });
            }
            expectedXmin = parsed;
        }

        // Ignore query filters to detect already-deleted (IsActive=false) contracts,
        // but keep tenant scoping explicitly.
        var contract = await db.Contracts
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.Id == contractId && c.CompanyId == tenantId, ct);

        if (contract is null)
        {
            return Results.NotFound();
        }

        // Idempotent delete: already deleted => 204
        if (!contract.IsActive)
        {
            return Results.NoContent();
        }

        if (expectedXmin.HasValue)
        {
            db.Entry(contract).Property("xmin").OriginalValue = expectedXmin.Value;
        }

        contract.Terminate(); // soft delete (tenant + isActive filter will hide it)

        try
        {
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            // If the caller provided If-Match, surface the concurrency conflict.
            // Otherwise treat as idempotent delete under concurrent requests.
            return hasIfMatch
                ? Results.StatusCode(StatusCodes.Status412PreconditionFailed)
                : Results.NoContent();
        }
    }

    private static bool TryParseEtagVersion(string rawIfMatchHeader, out uint version)
    {
        version = 0;
        var s = rawIfMatchHeader.Trim();

        // Accept: "123", W/"123", or multiple values like: "123", "456" (we take first)
        var commaIdx = s.IndexOf(',');
        if (commaIdx >= 0) s = s.Substring(0, commaIdx).Trim();

        if (s.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            s = s.Substring(2).TrimStart();
        }

        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
        {
            s = s.Substring(1, s.Length - 2);
        }

        return uint.TryParse(s, out version);
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

public sealed class UpdateContractRequest
{
    public string Name { get; set; } = null!;
}
