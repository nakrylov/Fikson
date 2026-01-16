using Microsoft.Extensions.Logging;

namespace Fixon.Infrastructure.BackgroundJobs.SlaRecalculation;

/// <summary>
/// Временная заглушка use case для пересчёта SLA.
/// Нужна, чтобы job pipeline был работоспособен без выдумывания бизнес-логики.
/// Реальную реализацию должен дать Application layer.
/// </summary>
public sealed class NoopSlaRecalculationUseCase : ISlaRecalculationUseCase
{
    private readonly ILogger<NoopSlaRecalculationUseCase> _logger;

    public NoopSlaRecalculationUseCase(ILogger<NoopSlaRecalculationUseCase> logger)
    {
        _logger = logger;
    }

    public Task RecalculateAsync(SlaRecalculationJobPayload payload, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "NOOP SLA recalculation. contractVersion={ContractVersionId} from={FromUtc} to={ToUtc}",
            payload.ContractVersionId, payload.FromUtc, payload.ToUtc);

        return Task.CompletedTask;
    }
}

