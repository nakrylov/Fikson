namespace Fixon.Infrastructure.BackgroundJobs.SlaRecalculation;

/// <summary>
/// Job handler вызывает use case. Никакой "job-only" бизнес-логики.
/// Tenant context обеспечивается BackgroundJobExecutor (FixedTenantProvider).
/// </summary>
public sealed class SlaRecalculationJobHandler : IBackgroundJobHandler
{
    private readonly ISlaRecalculationUseCase _useCase;

    public SlaRecalculationJobHandler(ISlaRecalculationUseCase useCase)
    {
        _useCase = useCase;
    }

    public string JobType => BackgroundJobType.SlaRecalculation;

    public async Task HandleAsync(BackgroundJobExecutionContext context)
    {
        var payload = SlaRecalculationJobPayload.Deserialize(context.Job.PayloadJson);
        await _useCase.RecalculateAsync(payload, context.CancellationToken);
    }
}

