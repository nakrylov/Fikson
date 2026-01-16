using Fixon.Domain.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class SlaRuleVersionConfiguration : IEntityTypeConfiguration<SlaRuleVersion>
{
    public void Configure(EntityTypeBuilder<SlaRuleVersion> builder)
    {
        builder.ToTable("sla_rule_versions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.SlaRuleId).IsRequired();
        builder.Property(x => x.VersionNumber).IsRequired();
        builder.Property(x => x.AppliesWhenJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ConditionJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.PenaltyJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.EffectiveFrom).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.EffectiveTo).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.CreatedByUserId).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasOne(x => x.SlaRule)
            .WithMany(r => r.Versions)
            .HasForeignKey(x => x.SlaRuleId);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId);

        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => new { x.SlaRuleId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.SlaRuleId });
    }
}


