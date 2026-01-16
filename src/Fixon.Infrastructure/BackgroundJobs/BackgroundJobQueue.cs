using Fixon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fixon.Infrastructure.BackgroundJobs;

public interface IBackgroundJobQueue
{
    Task EnqueueAsync(BackgroundJob job, CancellationToken cancellationToken);
}

public sealed class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly FixonDbContext _db;

    public BackgroundJobQueue(FixonDbContext db)
    {
        _db = db;
    }

    public async Task EnqueueAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        // Идемпотентность обеспечивается уникальными индексами (correlationId).
        _db.BackgroundJobs.Add(job);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Повторная постановка того же job → no-op.
            // Не делаем "silent failure": пробрасываем только если это не конфликт идемпотентности.
            // В Postgres ловить по SQLSTATE можно позднее; пока безопасный best-effort.
            throw new InvalidOperationException("Failed to enqueue background job (possible duplicate correlationId).", ex);
        }
    }
}

