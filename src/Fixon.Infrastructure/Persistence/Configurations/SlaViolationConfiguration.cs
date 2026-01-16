using Fixon.Domain.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class SlaViolationConfiguration : IEntityTypeConfiguration<SlaViolation>
{
    public void Configure(EntityTypeBuilder<SlaViolation> builder)
    {
        builder.ToTable("sla_violations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.ContractVersionId).IsRequired();
        builder.Property(x => x.SlaRuleVersionId).IsRequired();
        builder.Property(x => x.SlaEvaluationId).IsRequired();
        builder.Property(x => x.CalculatedValuesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DetectedAt).HasColumnType("timestamp with time zone").IsRequired();

        builder.HasOne(x => x.ContractVersion)
            .WithMany()
            .HasForeignKey(x => x.ContractVersionId);

        builder.HasOne(x => x.SlaRuleVersion)
            .WithMany()
            .HasForeignKey(x => x.SlaRuleVersionId);

        builder.HasOne(x => x.SlaEvaluation)
            .WithMany()
            .HasForeignKey(x => x.SlaEvaluationId);

        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => new { x.CompanyId, x.ContractVersionId });
        builder.HasIndex(x => new { x.CompanyId, x.SlaRuleVersionId });
        builder.HasIndex(x => new { x.CompanyId, x.SlaEvaluationId }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.DetectedAt });
    }
}

