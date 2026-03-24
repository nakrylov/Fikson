using Fixon.Domain.Claims;
using Fixon.Domain.Financial;
using Fixon.Domain.Penalties;
using Fixon.Infrastructure.Audit;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Fixon.Api.Endpoints;

public static class ClaimsEndpoints
{
    [Authorize(Policy = Permissions.ClaimsRead)]
    public static async Task<IResult> GetClaims(
        FixonDbContext db,
        CancellationToken ct)
    {
        var rows = await (
            from claim in db.Claims.AsNoTracking()
            join ruleVersion in db.SlaRuleVersions.AsNoTracking() on claim.SlaRuleVersionId equals ruleVersion.Id
            join rule in db.SlaRules.AsNoTracking() on ruleVersion.SlaRuleId equals rule.Id
            join violation in db.SlaViolations.AsNoTracking() on claim.SlaViolationId equals violation.Id
            orderby claim.OpenedAt descending
            select new
            {
                id = claim.Id,
                shipmentId = ExtractShipmentId(violation.CalculatedValuesJson),
                ruleId = ruleVersion.SlaRuleId,
                ruleName = rule.Name,
                conditionType = rule.ConditionType,
                minValue = rule.MinValue,
                maxValue = rule.MaxValue,
                eventType = rule.EventType,
                scopeJson = rule.ScopeJson,
                conditionJson = ruleVersion.ConditionJson,
                calculatedValuesJson = violation.CalculatedValuesJson,
                penaltyAmount = claim.Amount,
                currency = claim.Currency,
                createdAt = claim.OpenedAt
            })
            .ToListAsync(ct);

        var items = rows.Select(row => new
        {
            row.id,
            row.shipmentId,
            row.ruleId,
            metric = TryReadString(row.conditionJson, "metric") ?? row.ruleName,
            conditionType = string.IsNullOrWhiteSpace(row.conditionType) ? "threshold" : row.conditionType,
            @operator = TryReadString(row.conditionJson, "operator") ?? ">",
            threshold = TryReadDecimal(row.conditionJson, "threshold"),
            row.minValue,
            row.maxValue,
            row.eventType,
            scope = TryReadJsonElement(row.scopeJson),
            calculatedValues = TryReadJsonElement(row.calculatedValuesJson),
            row.penaltyAmount,
            row.currency,
            row.createdAt
        }).ToList();

        return Results.Ok(items);
    }

    [Authorize(Policy = Permissions.ClaimsRead)]
    public static async Task<IResult> GetClaimsSummary(
        FixonDbContext db,
        CancellationToken ct)
    {
        var totalClaims = await db.Claims.AsNoTracking().CountAsync(ct);
        var totalPenalty = await db.Claims.AsNoTracking().SumAsync(x => x.Amount, ct);

        var byRule = await (
            from claim in db.Claims.AsNoTracking()
            join ruleVersion in db.SlaRuleVersions.AsNoTracking() on claim.SlaRuleVersionId equals ruleVersion.Id
            group claim by ruleVersion.SlaRuleId
            into g
            select new
            {
                ruleId = g.Key,
                count = g.Count(),
                penalty = g.Sum(x => x.Amount)
            })
            .ToListAsync(ct);

        return Results.Ok(new
        {
            totalClaims,
            totalPenalty,
            byRule
        });
    }

    [Authorize(Policy = Permissions.ClaimsRead)]
    public static async Task<IResult> GetClaim(
        [FromRoute] Guid claimId,
        FixonDbContext db,
        CancellationToken ct)
    {
        var claim = await db.Claims
            .AsNoTracking()
            .Where(x => x.Id == claimId)
            .Select(x => new
            {
                x.Id,
                x.ContractId,
                x.ContractVersionId,
                x.SlaRuleVersionId,
                x.SlaViolationId,
                x.Status,
                x.Amount,
                x.Currency,
                x.OpenedAt,
                x.ClosedAt
            })
            .SingleOrDefaultAsync(ct);

        return claim is null ? Results.NotFound() : Results.Ok(claim);
    }

    [Authorize(Policy = Permissions.ClaimsRead)]
    public static async Task<IResult> GetClaimTimeline(
        [FromRoute] Guid claimId,
        FixonDbContext db,
        CancellationToken ct)
    {
        var claim = await db.Claims.AsNoTracking().SingleOrDefaultAsync(x => x.Id == claimId, ct);
        if (claim is null) return Results.NotFound();

        var decisions = await db.ClaimDecisions.AsNoTracking()
            .Where(x => x.ClaimId == claimId)
            .OrderBy(x => x.DecidedAt)
            .Select(x => new
            {
                x.Id,
                x.DecisionType,
                x.DecisionAmount,
                x.Currency,
                x.DecidedByUserId,
                x.DecidedAt,
                x.Comment
            })
            .ToListAsync(ct);

        var dispute = await db.Disputes.AsNoTracking()
            .Where(x => x.ClaimId == claimId)
            .Select(x => new
            {
                x.Id,
                x.InitiatedBy,
                x.Reason,
                x.Status,
                x.OpenedAt,
                x.ResolvedAt
            })
            .SingleOrDefaultAsync(ct);

        var penalty = await db.Penalties.AsNoTracking()
            .Where(x => x.ClaimId == claimId)
            .Select(x => new
            {
                x.ClaimId,
                x.CalculatedAmount,
                x.Currency,
                x.CalculatedAt,
                x.PenaltyJsonSnapshot,
                x.ExplanationJson
            })
            .SingleOrDefaultAsync(ct);

        var adjustments = await db.PenaltyAdjustments.AsNoTracking()
            .Where(x => x.ClaimId == claimId)
            .OrderBy(x => x.AdjustedAt)
            .Select(x => new
            {
                x.Id,
                x.OriginalPenaltyAmount,
                x.NewPenaltyAmount,
                x.Currency,
                x.Reason,
                x.AdjustedByUserId,
                x.AdjustedAt
            })
            .ToListAsync(ct);

        return Results.Ok(new
        {
            claim = new
            {
                claim.Id,
                claim.ContractId,
                claim.ContractVersionId,
                claim.SlaRuleVersionId,
                claim.SlaViolationId,
                claim.Status,
                claim.Amount,
                claim.Currency,
                claim.OpenedAt,
                claim.ClosedAt
            },
            decisions,
            dispute,
            penalty,
            adjustments
        });
    }

    [Authorize(Policy = Permissions.ClaimsManage)]
    public static async Task<IResult> SubmitClaim(
        [FromRoute] Guid claimId,
        FixonDbContext db,
        IAuditWriter audit,
        UserContext userContext,
        CancellationToken ct)
    {
        var claim = await db.Claims.SingleOrDefaultAsync(x => x.Id == claimId, ct);
        if (claim is null) return Results.NotFound();

        claim.Submit();
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditWriteRequest(
            CompanyId: userContext.TenantId,
            EntityType: "Claim",
            EntityId: claimId.ToString(),
            Action: "Submit",
            DetailsJson: JsonSerializer.Serialize(new { claimId })), ct);

        return Results.Ok(new { claimId, claim.Status });
    }

    [Authorize(Policy = Permissions.ClaimsManage)]
    public static async Task<IResult> ReviewClaim(
        [FromRoute] Guid claimId,
        FixonDbContext db,
        IAuditWriter audit,
        UserContext userContext,
        CancellationToken ct)
    {
        var claim = await db.Claims.SingleOrDefaultAsync(x => x.Id == claimId, ct);
        if (claim is null) return Results.NotFound();

        claim.MarkUnderReview();
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditWriteRequest(
            CompanyId: userContext.TenantId,
            EntityType: "Claim",
            EntityId: claimId.ToString(),
            Action: "UnderReview",
            DetailsJson: JsonSerializer.Serialize(new { claimId })), ct);

        return Results.Ok(new { claimId, claim.Status });
    }

    [Authorize(Policy = Permissions.ClaimsManage)]
    public static async Task<IResult> DecideClaim(
        [FromRoute] Guid claimId,
        [FromBody] DecideClaimRequest request,
        FixonDbContext db,
        IAuditWriter audit,
        UserContext userContext,
        CancellationToken ct)
    {
        var claim = await db.Claims.SingleOrDefaultAsync(x => x.Id == claimId, ct);
        if (claim is null) return Results.NotFound();

        var now = DateTimeOffset.UtcNow;

        // decision append-only
        var decision = new ClaimDecision(
            id: Guid.NewGuid(),
            companyId: userContext.TenantId,
            claimId: claimId,
            decisionType: request.DecisionType,
            decisionAmount: request.DecisionAmount,
            currency: claim.Currency,
            decidedByUserId: userContext.UserId,
            decidedAt: now,
            comment: request.Comment);

        db.ClaimDecisions.Add(decision);

        // update claim state
        switch (request.DecisionType)
        {
            case ClaimDecisionType.Accept:
            case ClaimDecisionType.PartialAccept:
                claim.MarkAccepted();
                break;
            case ClaimDecisionType.Reject:
                claim.MarkRejected(now);
                break;
            default:
                return Results.BadRequest(new { error = "Unknown decision type." });
        }

        // reflect financial effect via PenaltyAdjustment (no recalculation)
        if (request.DecisionType is ClaimDecisionType.Reject or ClaimDecisionType.PartialAccept)
        {
            var penalty = await db.Penalties.SingleOrDefaultAsync(p => p.ClaimId == claimId, ct);
            if (penalty is null) return Results.BadRequest(new { error = "Penalty not found for claim." });

            var original = new Money(penalty.CalculatedAmount, new CurrencyCode(penalty.Currency));
            var newAmount = request.DecisionType == ClaimDecisionType.Reject
                ? new Money(0m, original.Currency)
                : new Money(request.DecisionAmount, original.Currency);

            db.PenaltyAdjustments.Add(new PenaltyAdjustment(
                id: Guid.NewGuid(),
                companyId: userContext.TenantId,
                claimId: claimId,
                originalPenalty: original,
                newPenalty: newAmount,
                reason: request.Comment ?? $"Decision: {request.DecisionType}",
                adjustedByUserId: userContext.UserId,
                adjustedAt: now));
        }

        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditWriteRequest(
            CompanyId: userContext.TenantId,
            EntityType: "ClaimDecision",
            EntityId: decision.Id.ToString(),
            Action: "Create",
            DetailsJson: JsonSerializer.Serialize(new
            {
                claimId,
                decisionId = decision.Id,
                request.DecisionType,
                request.DecisionAmount
            })), ct);

        return Results.Ok(new { claimId, claim.Status, decisionId = decision.Id });
    }

    [Authorize(Policy = Permissions.ClaimsManage)]
    public static async Task<IResult> OpenDispute(
        [FromRoute] Guid claimId,
        [FromBody] OpenDisputeRequest request,
        FixonDbContext db,
        IAuditWriter audit,
        UserContext userContext,
        CancellationToken ct)
    {
        var claim = await db.Claims.SingleOrDefaultAsync(x => x.Id == claimId, ct);
        if (claim is null) return Results.NotFound();

        // one dispute per claim (unique constraint)
        var exists = await db.Disputes.AnyAsync(x => x.ClaimId == claimId, ct);
        if (exists) return Results.BadRequest(new { error = "Dispute already exists for this claim." });

        claim.MarkDisputed();
        var dispute = new Dispute(
            id: Guid.NewGuid(),
            companyId: userContext.TenantId,
            claimId: claimId,
            initiatedBy: request.InitiatedBy,
            reason: request.Reason,
            openedAt: DateTimeOffset.UtcNow);

        db.Disputes.Add(dispute);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditWriteRequest(
            CompanyId: userContext.TenantId,
            EntityType: "Dispute",
            EntityId: dispute.Id.ToString(),
            Action: "Open",
            DetailsJson: JsonSerializer.Serialize(new { claimId, disputeId = dispute.Id, request.InitiatedBy })), ct);

        return Results.Ok(new { claimId, claim.Status, disputeId = dispute.Id });
    }

    [Authorize(Policy = Permissions.ClaimsManage)]
    public static async Task<IResult> ResolveDispute(
        [FromRoute] Guid claimId,
        [FromRoute] Guid disputeId,
        FixonDbContext db,
        IAuditWriter audit,
        UserContext userContext,
        CancellationToken ct)
    {
        var claim = await db.Claims.SingleOrDefaultAsync(x => x.Id == claimId, ct);
        if (claim is null) return Results.NotFound();

        var dispute = await db.Disputes.SingleOrDefaultAsync(x => x.Id == disputeId && x.ClaimId == claimId, ct);
        if (dispute is null) return Results.NotFound();

        var now = DateTimeOffset.UtcNow;
        dispute.Resolve(now);
        claim.MarkResolved(now);

        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditWriteRequest(
            CompanyId: userContext.TenantId,
            EntityType: "Dispute",
            EntityId: disputeId.ToString(),
            Action: "Resolve",
            DetailsJson: JsonSerializer.Serialize(new { claimId, disputeId })), ct);

        return Results.Ok(new { claimId, claimStatus = claim.Status, disputeId, disputeStatus = dispute.Status });
    }

    [Authorize(Policy = Permissions.ClaimsManage)]
    public static async Task<IResult> CancelClaim(
        [FromRoute] Guid claimId,
        FixonDbContext db,
        IAuditWriter audit,
        UserContext userContext,
        CancellationToken ct)
    {
        var claim = await db.Claims.SingleOrDefaultAsync(x => x.Id == claimId, ct);
        if (claim is null) return Results.NotFound();

        var now = DateTimeOffset.UtcNow;
        claim.Cancel(now);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditWriteRequest(
            CompanyId: userContext.TenantId,
            EntityType: "Claim",
            EntityId: claimId.ToString(),
            Action: "Cancel",
            DetailsJson: JsonSerializer.Serialize(new { claimId })), ct);

        return Results.Ok(new { claimId, claim.Status });
    }

    private static string? ExtractShipmentId(string calculatedValuesJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(calculatedValuesJson);
            if (doc.RootElement.TryGetProperty("shipmentId", out var shipmentProp) &&
                shipmentProp.ValueKind == JsonValueKind.String)
            {
                return shipmentProp.GetString();
            }
        }
        catch (JsonException)
        {
            // Keep null when payload does not contain expected schema.
        }

        return null;
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
            // keep null
        }

        return null;
    }

    private static decimal? TryReadDecimal(string json, string propertyName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(propertyName, out var p))
            {
                return null;
            }

            if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var numberValue))
            {
                return numberValue;
            }

            if (p.ValueKind == JsonValueKind.String && decimal.TryParse(p.GetString(), out var stringValue))
            {
                return stringValue;
            }
        }
        catch (JsonException)
        {
            // keep null
        }

        return null;
    }

    private static object? TryReadJsonElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed class DecideClaimRequest
{
    public ClaimDecisionType DecisionType { get; set; }
    public decimal DecisionAmount { get; set; }
    public string? Comment { get; set; }
}

public sealed class OpenDisputeRequest
{
    public DisputeInitiatedBy InitiatedBy { get; set; }
    public string Reason { get; set; } = null!;
}

