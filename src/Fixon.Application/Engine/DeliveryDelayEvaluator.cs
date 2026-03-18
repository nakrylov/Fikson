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
            var delayValue = shipmentGroup.Max(x => x.DelayMinutes);

            var matching = rules
                .Where(r => IsMatched(delayValue, r.Operator, r.Threshold))
                .OrderByDescending(r => r.Threshold)
                .FirstOrDefault();

            if (matching is null)
            {
                continue;
            }

            results.Add(new DeliveryDelayMatch(
                ShipmentId: shipmentGroup.Key,
                DelayMinutes: delayValue,
                RuleId: matching.RuleId,
                RuleVersionId: matching.RuleVersionId,
                ContractId: matching.ContractId,
                ContractVersionId: matching.ContractVersionId,
                PenaltyAmount: matching.PenaltyAmount,
                Threshold: matching.Threshold));
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

public sealed record DeliveryDelayFactInput(string ShipmentId, double DelayMinutes);

public sealed record DeliveryDelayRuleInput(
    Guid RuleId,
    Guid RuleVersionId,
    Guid ContractId,
    Guid ContractVersionId,
    string Operator,
    double Threshold,
    decimal PenaltyAmount);

public sealed record DeliveryDelayMatch(
    string ShipmentId,
    double DelayMinutes,
    Guid RuleId,
    Guid RuleVersionId,
    Guid ContractId,
    Guid ContractVersionId,
    decimal PenaltyAmount,
    double Threshold);
