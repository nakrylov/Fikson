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
            .Where(f =>
                f.FactType == "DELIVERY_DELAY"
                || f.FactType == "TEMPERATURE"
                || f.FactType == "MISSING_DOCS"
                || f.FactType == "DOCUMENT_MISSING")
            .Select(f => new
            {
                f.FactType,
                f.ExternalReference,
                f.AttributesJson
            })
            .ToListAsync(ct);

        var facts = new List<DeliveryDelayFactInput>();
        foreach (var item in factsRaw)
        {
            if (!TryExtractDeliveryDelay(
                    item.FactType,
                    item.ExternalReference,
                    item.AttributesJson,
                    out var metric,
                    out var shipmentId,
                    out var value,
                    out var hasNumericValue,
                    out var eventType,
                    out var cargoType,
                    out var counterpartyId))
            {
                continue;
            }

            facts.Add(new DeliveryDelayFactInput(shipmentId, metric, value, hasNumericValue, eventType, cargoType, counterpartyId));
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
                rule.ScopeJson,
                rule.ConditionType,
                rule.MinValue,
                rule.MaxValue,
                rule.Name,
                rule.ContractId,
                ContractCounterpartyId = contract.CounterpartyId,
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
            if (!TryExtractRule(
                    item.Name,
                    item.ConditionJson,
                    item.PenaltyJson,
                    item.ScopeJson,
                    item.ConditionType,
                    item.MinValue,
                    item.MaxValue,
                    out var metric,
                    out var conditionType,
                    out var @operator,
                    out var threshold,
                    out var penaltyAmount,
                    out var cargoTypeScope,
                    out var eventType))
            {
                continue;
            }

            rules.Add(new DeliveryDelayRuleInput(
                RuleId: item.RuleId,
                RuleVersionId: item.Id,
                ContractId: item.ContractId,
                ContractVersionId: item.ContractVersionId,
                Metric: metric,
                ConditionType: conditionType,
                Operator: @operator,
                Threshold: threshold,
                PenaltyAmount: penaltyAmount,
                CargoTypeScope: cargoTypeScope,
                EventType: eventType,
                MinValue: conditionType == SlaRule.ConditionTypeRange ? Convert.ToDouble(item.MinValue ?? 0m) : null,
                MaxValue: conditionType == SlaRule.ConditionTypeRange ? Convert.ToDouble(item.MaxValue ?? 0m) : null,
                ContractCounterpartyId: item.ContractCounterpartyId));
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

    private static bool TryExtractDeliveryDelay(
        string factType,
        string? externalReference,
        string attributesJson,
        out string metric,
        out string shipmentId,
        out double value,
        out bool hasNumericValue,
        out string? eventType,
        out string? cargoType,
        out Guid? counterpartyId)
    {
        metric = string.Empty;
        shipmentId = string.Empty;
        value = 0;
        hasNumericValue = false;
        eventType = null;
        cargoType = null;
        counterpartyId = null;

        try
        {
            using var doc = JsonDocument.Parse(attributesJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("counterpartyId", out var counterpartyIdProp)
                && counterpartyIdProp.ValueKind == JsonValueKind.String
                && Guid.TryParse(counterpartyIdProp.GetString(), out var parsedCp))
            {
                counterpartyId = parsedCp;
            }

            if (root.TryGetProperty("shipmentId", out var shipmentIdProp) && shipmentIdProp.ValueKind == JsonValueKind.String)
            {
                shipmentId = shipmentIdProp.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("cargoType", out var cargoTypeProp) && cargoTypeProp.ValueKind == JsonValueKind.String)
            {
                cargoType = cargoTypeProp.GetString();
            }

            if (root.TryGetProperty("eventType", out var eventTypeProp) && eventTypeProp.ValueKind == JsonValueKind.String)
            {
                eventType = eventTypeProp.GetString();
            }

            if (string.IsNullOrWhiteSpace(shipmentId))
            {
                shipmentId = ExtractShipmentFromExternalReference(externalReference);
            }

            if (string.IsNullOrWhiteSpace(shipmentId))
            {
                return false;
            }

            metric = factType;
            if (!IsSupportedMetric(metric))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(eventType))
            {
                eventType = metric;
            }

            if (root.TryGetProperty("valueNumber", out var valueNumberProp))
            {
                if (valueNumberProp.ValueKind == JsonValueKind.Number && valueNumberProp.TryGetDouble(out var parsed))
                {
                    value = parsed;
                    hasNumericValue = true;
                    return true;
                }

                if (valueNumberProp.ValueKind == JsonValueKind.String &&
                    double.TryParse(valueNumberProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    value = parsed;
                    hasNumericValue = true;
                    return true;
                }
            }

            if (root.TryGetProperty("factValue", out var factValueProp) && factValueProp.ValueKind == JsonValueKind.String)
            {
                var factValue = factValueProp.GetString();
                if (double.TryParse(factValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    value = parsed;
                    hasNumericValue = true;
                    return true;
                }
            }

            return true;
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
        string? scopeJson,
        string? conditionTypeRaw,
        decimal? minValueRaw,
        decimal? maxValueRaw,
        out string metric,
        out string conditionType,
        out string @operator,
        out double threshold,
        out decimal penaltyAmount,
        out string? cargoTypeScope,
        out string? eventType)
    {
        metric = string.Empty;
        conditionType = SlaRule.ConditionTypeThreshold;
        @operator = ">";
        threshold = 0;
        penaltyAmount = 0;
        cargoTypeScope = null;
        eventType = null;

        try
        {
            using var conditionDoc = JsonDocument.Parse(conditionJson);
            var condition = conditionDoc.RootElement;

            metric =
                GetOptionalString(condition, "metric")
                ?? GetOptionalString(condition, "factType")
                ?? ruleName;

            if (!IsSupportedMetric(metric))
            {
                return false;
            }

            conditionType = string.IsNullOrWhiteSpace(conditionTypeRaw)
                ? SlaRule.ConditionTypeThreshold
                : conditionTypeRaw.Trim().ToLowerInvariant();

            if (conditionType != SlaRule.ConditionTypeThreshold && conditionType != SlaRule.ConditionTypeRange)
            {
                if (conditionType != SlaRule.ConditionTypeBoolean)
                {
                    return false;
                }
            }

            if (conditionType == SlaRule.ConditionTypeBoolean)
            {
                eventType = GetOptionalString(condition, "eventType");
                if (string.IsNullOrWhiteSpace(eventType))
                {
                    eventType = string.IsNullOrWhiteSpace(ruleName) ? null : ruleName;
                }
                if (string.IsNullOrWhiteSpace(eventType))
                {
                    return false;
                }
            }
            else
            {
                @operator = GetOptionalString(condition, "operator") ?? ">";

                if (conditionType == SlaRule.ConditionTypeThreshold && !TryGetDouble(condition, "threshold", out threshold))
                {
                    return false;
                }
            }

            using var penaltyDoc = JsonDocument.Parse(penaltyJson);
            var penalty = penaltyDoc.RootElement;

            if (!TryGetDecimal(penalty, "Value", out penaltyAmount) &&
                !TryGetDecimal(penalty, "value", out penaltyAmount) &&
                !TryGetDecimal(penalty, "penaltyAmount", out penaltyAmount))
            {
                return false;
            }

            if (conditionType == SlaRule.ConditionTypeRange && (!minValueRaw.HasValue || !maxValueRaw.HasValue || minValueRaw > maxValueRaw))
            {
                return false;
            }

            if (!TryGetScopeCargoType(scopeJson, out cargoTypeScope))
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

    private static bool IsSupportedMetric(string? metric)
    {
        return string.Equals(metric, "DELIVERY_DELAY", StringComparison.OrdinalIgnoreCase)
            || string.Equals(metric, "TEMPERATURE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(metric, "MISSING_DOCS", StringComparison.OrdinalIgnoreCase)
            || string.Equals(metric, "DOCUMENT_MISSING", StringComparison.OrdinalIgnoreCase);
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

    private static bool TryGetScopeCargoType(string? scopeJson, out string? cargoTypeScope)
    {
        cargoTypeScope = null;
        if (string.IsNullOrWhiteSpace(scopeJson))
        {
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(scopeJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (root.TryGetProperty("cargoType", out var cargoTypeProp) && cargoTypeProp.ValueKind == JsonValueKind.String)
            {
                cargoTypeScope = cargoTypeProp.GetString();
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
