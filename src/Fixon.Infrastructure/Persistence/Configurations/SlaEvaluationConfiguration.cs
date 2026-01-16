using Fixon.Domain.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class SlaEvaluationConfiguration : IEntityTypeConfiguration<SlaEvaluation>
{
    public void Configure(EntityTypeBuilder<SlaEvaluation> builder)
    {
        builder.ToTable("sla_evaluations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.ContractVersionId).IsRequired();
        builder.Property(x => x.SlaRuleVersionId).IsRequired();
        builder.Property(x => x.EvaluationResult).HasConversion<int>().IsRequired();
        builder.Property(x => x.CalculatedValuesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.EvaluatedAt).HasColumnType("timestamp with time zone").IsRequired();

        builder.HasOne(x => x.ContractVersion)
            .WithMany(v => v.SlaEvaluations)
            .HasForeignKey(x => x.ContractVersionId);

        builder.HasOne(x => x.SlaRuleVersion)
            .WithMany(v => v.SlaEvaluations)
            .HasForeignKey(x => x.SlaRuleVersionId);

        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => new { x.CompanyId, x.EvaluatedAt });
        builder.HasIndex(x => new { x.CompanyId, x.ContractVersionId });
        builder.HasIndex(x => new { x.CompanyId, x.SlaRuleVersionId });
    }
}


