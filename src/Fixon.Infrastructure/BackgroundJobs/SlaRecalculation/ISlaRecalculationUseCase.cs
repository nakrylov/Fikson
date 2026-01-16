namespace Fixon.Infrastructure.BackgroundJobs.SlaRecalculation;

/// <summary>
/// Application use case интерфейс для пересчёта SLA.
/// Job handler не содержит бизнес-логики и делегирует сюда.
/// </summary>
public interface ISlaRecalculationUseCase
{
    Task RecalculateAsync(SlaRecalculationJobPayload payload, CancellationToken cancellationToken);
}

