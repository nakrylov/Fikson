using Fixon.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.ActorType).HasConversion<int>().IsRequired();
        builder.Property(x => x.ActorUserId);
        builder.Property(x => x.ActorSystem).HasMaxLength(200);
        builder.Property(x => x.EntityType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(200);
        builder.Property(x => x.DetailsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.PreviousAuditLogId);
        builder.Property(x => x.Timestamp).HasColumnType("timestamp with time zone").IsRequired();

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId);

        builder.HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId);

        builder.HasOne(x => x.Previous)
            .WithMany()
            .HasForeignKey(x => x.PreviousAuditLogId);

        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => new { x.CompanyId, x.Timestamp });
        builder.HasIndex(x => new { x.CompanyId, x.EntityType, x.EntityId });
        builder.HasIndex(x => new { x.CompanyId, x.ActorUserId, x.Timestamp });
        builder.HasIndex(x => new { x.CompanyId, x.CorrelationId });
    }
}


