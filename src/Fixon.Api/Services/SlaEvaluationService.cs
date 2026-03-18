using System.Globalization;
using System.Text.Json;
using Fixon.Application.Engine;
using Fixon.Domain.Claims;
using Fixon.Domain.Contracts;
using Fixon.Domain.Sla;
using Fixon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fixon.Api.Services;

public sealed class SlaEvaluationService
{
    private readonly FixonDbContext _db;

    public SlaEvaluationService(FixonDbContext db)
    {
        _db = db;
    }

    public async Task<int> RunEvaluationForTenant(Guid tenantId, Guid createdByUserId, CancellationToken ct)
    {
        var factsRaw = await _db.Facts
            .AsNoTracking()
            .Where(f => f.FactType == "DELIVERY_DELAY")
            .Select(f => new
            {
                f.ExternalReference,
                f.AttributesJson
            })
            .ToListAsync(ct);

        var facts = new List<DeliveryDelayFactInput>();
        foreach (var item in factsRaw)
        {
            if (!TryExtractDeliveryDelay(item.ExternalReference, item.AttributesJson, out var shipmentId, out var delayMinutes))
            {
                continue;
            }

            facts.Add(new DeliveryDelayFactInput(shipmentId, delayMinutes));
        }

        if (facts.Count == 0)
        {
            return 0;
        }

        var ruleCandidates = await (
            from ruleVersion in _db.SlaRuleVersions.AsNoTracking()
            join rule in _db.SlaRules.AsNoTracking() on ruleVersion.SlaRuleId equals rule.Id
            join contract in _db.Contracts.AsNoTracking() on rule.ContractId equals contract.Id
            where ruleVersion.IsActive
                  && rule.IsActive
                  && contract.Status == ContractStatus.Active
                  && contract.CurrentVersionId != null
            select new
            {
                RuleId = rule.Id,
                ruleVersion.Id,
                ruleVersion.VersionNumber,
                ruleVersion.ConditionJson,
                ruleVersion.PenaltyJson,
                rule.Name,
                rule.ContractId,
                ContractVersionId = contract.CurrentVersionId!.Value
            })
            .ToListAsync(ct);

        var latestRules = ruleCandidates
            .GroupBy(x => x.RuleId)
            .Select(g => g.OrderByDescending(x => x.VersionNumber).First())
            .ToList();

        var rules = new List<DeliveryDelayRuleInput>();
        foreach (var item in latestRules)
        {
            if (!TryExtractRule(item.Name, item.ConditionJson, item.PenaltyJson, out var @operator, out var threshold, out var penaltyAmount))
            {
                continue;
            }

            rules.Add(new DeliveryDelayRuleInput(
                RuleId: item.RuleId,
                RuleVersionId: item.Id,
                ContractId: item.ContractId,
                ContractVersionId: item.ContractVersionId,
                Operator: @operator,
                Threshold: threshold,
                PenaltyAmount: penaltyAmount));
        }

        if (rules.Count == 0)
        {
            return 0;
        }

        var candidateRuleIds = rules.Select(r => r.RuleId).Distinct().ToHashSet();
        var existingRows = await (
            from claim in _db.Claims.AsNoTracking()
            join ruleVersion in _db.SlaRuleVersions.AsNoTracking() on claim.SlaRuleVersionId equals ruleVersion.Id
            join violation in _db.SlaViolations.AsNoTracking() on claim.SlaViolationId equals violation.Id
            where candidateRuleIds.Contains(ruleVersion.SlaRuleId)
            select new
            {
                RuleId = ruleVersion.SlaRuleId,
                violation.CalculatedValuesJson
            })
            .ToListAsync(ct);

        var existingKeys = new HashSet<(string ShipmentId, Guid RuleId)>();
        foreach (var row in existingRows)
        {
            if (!TryExtractShipmentIdFromCalculatedValues(row.CalculatedValuesJson, out var shipmentId))
            {
                continue;
            }

            existingKeys.Add((shipmentId, row.RuleId));
        }

        var evaluator = new DeliveryDelayEvaluator();
        var matches = evaluator.Evaluate(facts, rules);

        var now = DateTimeOffset.UtcNow;
        var generated = 0;

        foreach (var match in matches)
        {
            var dedupeKey = (match.ShipmentId, match.RuleId);
            if (existingKeys.Contains(dedupeKey))
            {
                continue;
            }

            var calculatedValuesJson = JsonSerializer.Serialize(new
            {
                shipmentId = match.ShipmentId,
                delayMinutes = match.DelayMinutes,
                threshold = match.Threshold
            });

            var evaluationId = Guid.NewGuid();
            var evaluation = new SlaEvaluation(
                id: evaluationId,
                companyId: tenantId,
                contractVersionId: match.ContractVersionId,
                slaRuleVersionId: match.RuleVersionId,
                evaluationResult: SlaEvaluationResult.Fail,
                calculatedValuesJson: calculatedValuesJson,
                evaluatedAt: now);

            var violationId = Guid.NewGuid();
            var violation = new SlaViolation(
                id: violationId,
                companyId: tenantId,
                contractVersionId: match.ContractVersionId,
                slaRuleVersionId: match.RuleVersionId,
                slaEvaluationId: evaluationId,
                calculatedValuesJson: calculatedValuesJson,
                detectedAt: now);

            var claim = new Fixon.Domain.Claims.Claim(
                id: Guid.NewGuid(),
                companyId: tenantId,
                contractId: match.ContractId,
                contractVersionId: match.ContractVersionId,
                slaRuleVersionId: match.RuleVersionId,
                status: ClaimStatus.Draft,
                amount: match.PenaltyAmount,
                currency: "USD",
                openedAt: now,
                closedAt: null,
                slaViolationId: violationId,
                createdByUserId: createdByUserId);

            _db.SlaEvaluations.Add(evaluation);
            _db.SlaViolations.Add(violation);
            _db.Claims.Add(claim);
            existingKeys.Add(dedupeKey);
            generated++;
        }

        if (generated > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return generated;
    }

    private static bool TryExtractDeliveryDelay(string? externalReference, string attributesJson, out string shipmentId, out double delayMinutes)
    {
        shipmentId = string.Empty;
        delayMinutes = 0;

        try
        {
            using var doc = JsonDocument.Parse(attributesJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("shipmentId", out var shipmentIdProp) && shipmentIdProp.ValueKind == JsonValueKind.String)
            {
                shipmentId = shipmentIdProp.GetString() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(shipmentId))
            {
                shipmentId = ExtractShipmentFromExternalReference(externalReference);
            }

            if (string.IsNullOrWhiteSpace(shipmentId))
            {
                return false;
            }

            if (root.TryGetProperty("valueNumber", out var valueNumberProp))
            {
                if (valueNumberProp.ValueKind == JsonValueKind.Number && valueNumberProp.TryGetDouble(out var parsed))
                {
                    delayMinutes = parsed;
                    return true;
                }

                if (valueNumberProp.ValueKind == JsonValueKind.String &&
                    double.TryParse(valueNumberProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    delayMinutes = parsed;
                    return true;
                }
            }

            if (root.TryGetProperty("factValue", out var factValueProp) && factValueProp.ValueKind == JsonValueKind.String)
            {
                var factValue = factValueProp.GetString();
                if (double.TryParse(factValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    delayMinutes = parsed;
                    return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ExtractShipmentFromExternalReference(string? externalReference)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
        {
            return string.Empty;
        }

        var separatorIndex = externalReference.IndexOf(':');
        return separatorIndex > 0
            ? externalReference[..separatorIndex]
            : externalReference;
    }

    private static bool TryExtractRule(
        string ruleName,
        string conditionJson,
        string penaltyJson,
        out string @operator,
        out double threshold,
        out decimal penaltyAmount)
    {
        @operator = ">";
        threshold = 0;
        penaltyAmount = 0;

        try
        {
            using var conditionDoc = JsonDocument.Parse(conditionJson);
            var condition = conditionDoc.RootElement;

            var metric =
                GetOptionalString(condition, "metric")
                ?? GetOptionalString(condition, "factType")
                ?? ruleName;

            if (!string.Equals(metric, "DELIVERY_DELAY", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            @operator = GetOptionalString(condition, "operator") ?? ">";

            if (!TryGetDouble(condition, "threshold", out threshold))
            {
                return false;
            }

            using var penaltyDoc = JsonDocument.Parse(penaltyJson);
            var penalty = penaltyDoc.RootElement;

            if (!TryGetDecimal(penalty, "Value", out penaltyAmount) &&
                !TryGetDecimal(penalty, "value", out penaltyAmount) &&
                !TryGetDecimal(penalty, "penaltyAmount", out penaltyAmount))
            {
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryExtractShipmentIdFromCalculatedValues(string json, out string shipmentId)
    {
        shipmentId = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("shipmentId", out var p) && p.ValueKind == JsonValueKind.String)
            {
                shipmentId = p.GetString() ?? string.Empty;
            }

            return !string.IsNullOrWhiteSpace(shipmentId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? GetOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var p))
        {
            return null;
        }

        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.GetRawText(),
            _ => null
        };
    }

    private static bool TryGetDouble(JsonElement root, string propertyName, out double value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var p))
        {
            return false;
        }

        if (p.ValueKind == JsonValueKind.Number)
        {
            return p.TryGetDouble(out value);
        }

        if (p.ValueKind == JsonValueKind.String)
        {
            return double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        return false;
    }

    private static bool TryGetDecimal(JsonElement root, string propertyName, out decimal value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var p))
        {
            return false;
        }

        if (p.ValueKind == JsonValueKind.Number)
        {
            return p.TryGetDecimal(out value);
        }

        if (p.ValueKind == JsonValueKind.String)
        {
            return decimal.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        return false;
    }
}
