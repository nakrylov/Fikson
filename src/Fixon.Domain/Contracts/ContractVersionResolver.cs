namespace Fixon.Domain.Contracts;

/// <summary>
/// Explicit deterministic contract version selection.
/// НЕ использует DateTime.Now. Выбор делается только по переданному event time.
/// </summary>
public static class ContractVersionResolver
{
    public static ContractVersion ResolveForEventUtc(
        IReadOnlyCollection<ContractVersion> versions,
        DateTimeOffset eventTimeUtc)
    {
        if (versions.Count == 0) throw new InvalidOperationException("Contract has no versions.");

        var candidates = versions
            .Where(v =>
                v.Status == ContractVersionStatus.Signed &&
                v.EffectiveFrom <= eventTimeUtc &&
                (v.EffectiveTo is null || v.EffectiveTo > eventTimeUtc))
            .OrderByDescending(v => v.VersionNumber)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No signed contract version covers the event time.");
        }

        // protection: must be unique (no overlaps)
        if (candidates.Count > 1 && candidates[0].EffectiveFrom == candidates[1].EffectiveFrom)
        {
            throw new InvalidOperationException("Ambiguous contract version selection: multiple signed versions match.");
        }

        return candidates[0];
    }
}

