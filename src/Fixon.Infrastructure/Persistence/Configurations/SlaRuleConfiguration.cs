using Fixon.Domain.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class SlaRuleConfiguration : IEntityTypeConfiguration<SlaRule>
{
    public void Configure(EntityTypeBuilder<SlaRule> builder)
    {
        builder.ToTable("sla_rules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.ContractId).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ScopeJson)
            .HasColumnName("scope_json")
            .HasColumnType("jsonb")
            .IsRequired(false);
        builder.Property(x => x.ConditionType)
            .HasColumnName("condition_type")
            .HasMaxLength(32)
            .HasDefaultValue(SlaRule.ConditionTypeThreshold)
            .IsRequired();
        builder.Property(x => x.MinValue)
            .HasColumnName("min_value")
            .HasColumnType("numeric")
            .IsRequired(false);
        builder.Property(x => x.MaxValue)
            .HasColumnName("max_value")
            .HasColumnType("numeric")
            .IsRequired(false);
        builder.Property(x => x.EventType)
            .HasColumnName("event_type")
            .HasColumnType("text")
            .IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();

        builder.HasOne(x => x.Contract)
            .WithMany(c => c.SlaRules)
            .HasForeignKey(x => x.ContractId);

        builder.HasMany(x => x.Versions)
            .WithOne(v => v.SlaRule)
            .HasForeignKey(v => v.SlaRuleId);

        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => new { x.CompanyId, x.ContractId });
    }
}


