using Fixon.Infrastructure.Persistence;
using Fixon.Infrastructure.Tenancy;

namespace Fixon.Infrastructure.BackgroundJobs;

public sealed record BackgroundJobExecutionContext(
    BackgroundJob Job,
    ITenantProvider TenantProvider,
    FixonDbContext DbContext,
    CancellationToken CancellationToken);

