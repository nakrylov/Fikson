using System.Text.Json;

namespace Fixon.Infrastructure.BackgroundJobs.SlaRecalculation;

/// <summary>
/// Payload для SLA recalculation/backfill.
/// Scope: окно времени, чтобы не делать "пересчитать всё" (см. требования).
/// </summary>
public sealed record SlaRecalculationJobPayload(
    Guid ContractVersionId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc)
{
    public static string Serialize(SlaRecalculationJobPayload payload)
        => JsonSerializer.Serialize(payload);

    public static SlaRecalculationJobPayload Deserialize(string json)
        => JsonSerializer.Deserialize<SlaRecalculationJobPayload>(json)
           ?? throw new FormatException("Invalid SLA recalculation payload.");
}

