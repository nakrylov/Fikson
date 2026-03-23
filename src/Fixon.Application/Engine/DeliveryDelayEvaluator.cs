namespace Fixon.Application.Engine;

public sealed class DeliveryDelayEvaluator
{
    public IReadOnlyList<DeliveryDelayMatch> Evaluate(
        IReadOnlyCollection<DeliveryDelayFactInput> facts,
        IReadOnlyCollection<DeliveryDelayRuleInput> rules)
    {
        if (facts.Count == 0 || rules.Count == 0)
        {
            return [];
        }

        var results = new List<DeliveryDelayMatch>();

        foreach (var shipmentGroup in facts.GroupBy(f => f.ShipmentId, StringComparer.OrdinalIgnoreCase))
        {
            var firstFact = shipmentGroup.First();
            var cargoType = shipmentGroup
                .Select(x => x.CargoType)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            var applicableRules = rules
                .Where(r => string.IsNullOrWhiteSpace(r.CargoTypeScope)
                    || string.Equals(cargoType, r.CargoTypeScope, StringComparison.OrdinalIgnoreCase));

            foreach (var rule in applicableRules)
            {
                if (string.Equals(rule.ConditionType, "boolean", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(rule.EventType))
                    {
                        continue;
                    }

                    var eventMatched = shipmentGroup.Any(f =>
                        !string.IsNullOrWhiteSpace(f.EventType)
                        && string.Equals(f.EventType, rule.EventType, StringComparison.OrdinalIgnoreCase));

                    if (!eventMatched)
                    {
                        continue;
                    }

                    results.Add(new DeliveryDelayMatch(
                        ShipmentId: firstFact.ShipmentId,
                        DelayMinutes: 0,
                        RuleId: rule.RuleId,
                        RuleVersionId: rule.RuleVersionId,
                        ContractId: rule.ContractId,
                        ContractVersionId: rule.ContractVersionId,
                        PenaltyAmount: rule.PenaltyAmount,
                        Threshold: 0));
                    continue;
                }

                var metricFacts = shipmentGroup
                    .Where(f => string.Equals(f.Metric, rule.Metric, StringComparison.OrdinalIgnoreCase))
                    .Where(f => f.HasNumericValue)
                    .ToList();

                if (metricFacts.Count == 0)
                {
                    continue;
                }

                if (string.Equals(rule.ConditionType, "range", StringComparison.OrdinalIgnoreCase))
                {
                    if (!rule.MinValue.HasValue || !rule.MaxValue.HasValue)
                    {
                        continue;
                    }

                    var outside = metricFacts.FirstOrDefault(f => f.Value < rule.MinValue.Value || f.Value > rule.MaxValue.Value);
                    if (outside is null)
                    {
                        continue;
                    }

                    results.Add(new DeliveryDelayMatch(
                        ShipmentId: firstFact.ShipmentId,
                        DelayMinutes: outside.Value,
                        RuleId: rule.RuleId,
                        RuleVersionId: rule.RuleVersionId,
                        ContractId: rule.ContractId,
                        ContractVersionId: rule.ContractVersionId,
                        PenaltyAmount: rule.PenaltyAmount,
                        Threshold: rule.Threshold));
                    continue;
                }

                var representativeValue = metricFacts.Max(x => x.Value);
                if (!IsMatched(representativeValue, rule.Operator, rule.Threshold))
                {
                    continue;
                }

                results.Add(new DeliveryDelayMatch(
                    ShipmentId: firstFact.ShipmentId,
                    DelayMinutes: representativeValue,
                    RuleId: rule.RuleId,
                    RuleVersionId: rule.RuleVersionId,
                    ContractId: rule.ContractId,
                    ContractVersionId: rule.ContractVersionId,
                    PenaltyAmount: rule.PenaltyAmount,
                    Threshold: rule.Threshold));
            }
        }

        return results;
    }

    private static bool IsMatched(double value, string @operator, double threshold)
    {
        return @operator switch
        {
            ">" => value > threshold,
            ">=" => value >= threshold,
            "<" => value < threshold,
            "<=" => value <= threshold,
            _ => value > threshold
        };
    }
}

public sealed record DeliveryDelayFactInput(
    string ShipmentId,
    string Metric,
    double Value,
    bool HasNumericValue,
    string? EventType,
    string? CargoType);

public sealed record DeliveryDelayRuleInput(
    Guid RuleId,
    Guid RuleVersionId,
    Guid ContractId,
    Guid ContractVersionId,
    string Metric,
    string ConditionType,
    string Operator,
    double Threshold,
    decimal PenaltyAmount,
    string? CargoTypeScope,
    string? EventType,
    double? MinValue,
    double? MaxValue);

public sealed record DeliveryDelayMatch(
    string ShipmentId,
    double DelayMinutes,
    Guid RuleId,
    Guid RuleVersionId,
    Guid ContractId,
    Guid ContractVersionId,
    decimal PenaltyAmount,
    double Threshold);
