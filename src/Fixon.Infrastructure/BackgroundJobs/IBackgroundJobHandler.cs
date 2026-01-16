namespace Fixon.Infrastructure.BackgroundJobs;

/// <summary>
/// Job handler НЕ содержит бизнес-логики: он вызывает use case.
/// </summary>
public interface IBackgroundJobHandler
{
    string JobType { get; }
    Task HandleAsync(BackgroundJobExecutionContext context);
}

