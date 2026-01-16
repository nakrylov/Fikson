using Fixon.Infrastructure.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.ToTable("background_jobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CompanyId);
        builder.Property(x => x.IsSystem).IsRequired();
        builder.Property(x => x.JobType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();

        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.MaxAttempts).IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ScheduledAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.StartedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");

        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.LockedBy).HasMaxLength(200);
        builder.Property(x => x.LockedAt).HasColumnType("timestamp with time zone");

        // Индексы для поиска задач
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.Status, x.ScheduledAt });
        builder.HasIndex(x => new { x.IsSystem, x.CompanyId, x.Status, x.ScheduledAt });

        // Идемпотентность: уникальность correlationId в пределах (tenant, jobType) или (system, jobType).
        // Postgres: NULL не участвует в уникальности → используем partial indexes.
        builder.HasIndex(x => new { x.JobType, x.CorrelationId })
            .IsUnique()
            .HasFilter("\"IsSystem\" = true");

        builder.HasIndex(x => new { x.CompanyId, x.JobType, x.CorrelationId })
            .IsUnique()
            .HasFilter("\"IsSystem\" = false");

        // Ограничения целостности
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_background_jobs_system_tenant",
                "(\"IsSystem\" = true AND \"CompanyId\" IS NULL) OR (\"IsSystem\" = false AND \"CompanyId\" IS NOT NULL)");
        });
    }
}

