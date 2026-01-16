using Fixon.Domain.Abstractions;

namespace Fixon.Infrastructure.BackgroundJobs;

/// <summary>
/// Инфраструктурная сущность для очереди фоновых задач.
/// Не является доменной сущностью Fixon (не описана в /docs).
/// Используется для идемпотентного выполнения use cases в фоне.
/// </summary>
public sealed class BackgroundJob : Entity
{
    /// <summary>
    /// TenantId (CompanyId) для tenant-aware job.
    /// Если IsSystem == true, может быть null.
    /// </summary>
    public Guid? CompanyId { get; private set; }

    /// <summary>
    /// System job (bypass tenant фильтров). Для обычных jobs = false.
    /// </summary>
    public bool IsSystem { get; private set; }

    public string JobType { get; private set; } = null!;

    /// <summary>
    /// CorrelationId — ключ идемпотентности (в пределах tenant + jobType).
    /// Повторная постановка того же correlationId не должна создавать дубль.
    /// </summary>
    public string CorrelationId { get; private set; } = null!;

    /// <summary>
    /// Payload в JSON. Job handler сам интерпретирует.
    /// </summary>
    public string PayloadJson { get; private set; } = null!;

    public BackgroundJobStatus Status { get; private set; }

    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ScheduledAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>
    /// Lock owner (для простого single-host сценария достаточно).
    /// </summary>
    public string? LockedBy { get; private set; }

    public DateTimeOffset? LockedAt { get; private set; }

    private BackgroundJob() { } // EF

    public BackgroundJob(
        Guid id,
        Guid? companyId,
        bool isSystem,
        string jobType,
        string correlationId,
        string payloadJson,
        DateTimeOffset createdAt,
        DateTimeOffset? scheduledAt,
        int maxAttempts = 5)
    {
        if (isSystem == false && companyId == null)
        {
            throw new ArgumentException("Tenant-aware job requires CompanyId.", nameof(companyId));
        }

        if (string.IsNullOrWhiteSpace(jobType)) throw new ArgumentException("JobType is required.", nameof(jobType));
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("CorrelationId is required.", nameof(correlationId));
        if (string.IsNullOrWhiteSpace(payloadJson)) throw new ArgumentException("PayloadJson is required.", nameof(payloadJson));
        if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        Id = id;
        CompanyId = companyId;
        IsSystem = isSystem;
        JobType = jobType;
        CorrelationId = correlationId;
        PayloadJson = payloadJson;
        CreatedAt = createdAt;
        ScheduledAt = scheduledAt;
        MaxAttempts = maxAttempts;
        Status = BackgroundJobStatus.Pending;
    }

    public bool CanRun(DateTimeOffset nowUtc)
    {
        if (Status != BackgroundJobStatus.Pending) return false;
        if (ScheduledAt.HasValue && ScheduledAt.Value > nowUtc) return false;
        return true;
    }

    public void MarkRunning(string lockedBy, DateTimeOffset nowUtc)
    {
        Status = BackgroundJobStatus.Running;
        StartedAt ??= nowUtc;
        LockedBy = lockedBy;
        LockedAt = nowUtc;
    }

    public void MarkSucceeded(DateTimeOffset nowUtc)
    {
        Status = BackgroundJobStatus.Succeeded;
        CompletedAt = nowUtc;
        LockedBy = null;
        LockedAt = null;
        LastError = null;
    }

    public void MarkFailed(string error, DateTimeOffset nowUtc, DateTimeOffset? rescheduleAtUtc)
    {
        AttemptCount++;
        LastError = error;

        LockedBy = null;
        LockedAt = null;

        if (AttemptCount >= MaxAttempts)
        {
            Status = BackgroundJobStatus.Failed;
            CompletedAt = nowUtc;
            return;
        }

        // safe retry: возвращаем в Pending и переносим ScheduledAt
        Status = BackgroundJobStatus.Pending;
        ScheduledAt = rescheduleAtUtc ?? nowUtc;
    }
}

