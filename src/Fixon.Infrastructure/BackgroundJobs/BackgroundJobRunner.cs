using Fixon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Fixon.Infrastructure.Audit;
using System.Text.Json;

namespace Fixon.Infrastructure.BackgroundJobs;

/// <summary>
/// Job execution pipeline (polling).
/// Важно: это инфраструктура. Бизнес-логика живёт в use cases / domain.
/// </summary>
public sealed class BackgroundJobRunner
{
    private readonly DbContextOptions<FixonDbContext> _dbOptions;
    private readonly BackgroundJobExecutor _executor;
    private readonly BackgroundJobRunnerOptions _options;
    private readonly ILogger<BackgroundJobRunner> _logger;
    private readonly IAuditWriter _audit;

    public BackgroundJobRunner(
        DbContextOptions<FixonDbContext> dbOptions,
        BackgroundJobExecutor executor,
        BackgroundJobRunnerOptions options,
        ILogger<BackgroundJobRunner> logger,
        IAuditWriter audit)
    {
        _dbOptions = dbOptions;
        _executor = executor;
        _options = options;
        _logger = logger;
        _audit = audit;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        // System context для доступа к очереди background_jobs (не tenant-scoped)
        await using var db = new FixonDbContext(_dbOptions, new Tenancy.FixedTenantProvider(null, isSystemContext: true));

        var now = DateTimeOffset.UtcNow;

        // Простая стратегия: берём 1 pending job.
        // Для multi-host потребуется SELECT ... FOR UPDATE SKIP LOCKED (позже).
        var job = await db.BackgroundJobs
            .Where(j => j.Status == BackgroundJobStatus.Pending)
            .Where(j => j.ScheduledAt == null || j.ScheduledAt <= now)
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job == null) return;

        // Claim job
        job.MarkRunning(_options.WorkerId, now);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            if (job.CompanyId.HasValue)
            {
                await _audit.WriteAsync(new AuditWriteRequest(
                    CompanyId: job.CompanyId.Value,
                    EntityType: "BackgroundJob",
                    EntityId: job.Id.ToString(),
                    Action: "Start",
                    DetailsJson: JsonSerializer.Serialize(new
                    {
                        jobType = job.JobType,
                        correlationId = job.CorrelationId,
                        scheduledAt = job.ScheduledAt,
                    }),
                    CorrelationId: job.CorrelationId), cancellationToken);
            }

            _logger.LogInformation("Running job {JobId} {JobType} tenant={TenantId} system={IsSystem} corr={CorrelationId}",
                job.Id, job.JobType, job.CompanyId, job.IsSystem, job.CorrelationId);

            await _executor.ExecuteAsync(job, cancellationToken);

            // Reload job row (system context)
            await db.Entry(job).ReloadAsync(cancellationToken);
            job.MarkSucceeded(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);

            if (job.CompanyId.HasValue)
            {
                await _audit.WriteAsync(new AuditWriteRequest(
                    CompanyId: job.CompanyId.Value,
                    EntityType: "BackgroundJob",
                    EntityId: job.Id.ToString(),
                    Action: "Success",
                    DetailsJson: JsonSerializer.Serialize(new
                    {
                        jobType = job.JobType,
                        correlationId = job.CorrelationId,
                        attempts = job.AttemptCount,
                    }),
                    CorrelationId: job.CorrelationId), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await db.Entry(job).ReloadAsync(cancellationToken);

            var attempt = job.AttemptCount + 1;
            var delay = ComputeBackoff(attempt);
            var rescheduleAt = DateTimeOffset.UtcNow.Add(delay);

            job.MarkFailed(Truncate(ex.ToString(), 2000), DateTimeOffset.UtcNow, rescheduleAt);
            await db.SaveChangesAsync(cancellationToken);

            if (job.CompanyId.HasValue)
            {
                await _audit.WriteAsync(new AuditWriteRequest(
                    CompanyId: job.CompanyId.Value,
                    EntityType: "BackgroundJob",
                    EntityId: job.Id.ToString(),
                    Action: "Fail",
                    DetailsJson: JsonSerializer.Serialize(new
                    {
                        jobType = job.JobType,
                        correlationId = job.CorrelationId,
                        attempt = attempt,
                        maxAttempts = job.MaxAttempts,
                        nextRun = job.ScheduledAt,
                        error = Truncate(ex.Message, 500),
                    }),
                    CorrelationId: job.CorrelationId), cancellationToken);
            }

            _logger.LogError(ex, "Job failed {JobId} {JobType} attempt={Attempt}/{MaxAttempts} next={Next}",
                job.Id, job.JobType, attempt, job.MaxAttempts, job.ScheduledAt);
        }
    }

    private static TimeSpan ComputeBackoff(int attempt)
    {
        // safe retry: bounded exponential backoff
        var seconds = Math.Min(300, Math.Pow(2, Math.Max(0, attempt - 1)));
        return TimeSpan.FromSeconds(seconds);
    }

    private static string Truncate(string value, int maxLen)
    {
        return value.Length <= maxLen ? value : value.Substring(0, maxLen);
    }
}

