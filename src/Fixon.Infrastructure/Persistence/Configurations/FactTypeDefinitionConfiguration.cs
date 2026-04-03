using Fixon.Domain.Facts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class FactTypeDefinitionConfiguration : IEntityTypeConfiguration<FactTypeDefinition>
{
    public void Configure(EntityTypeBuilder<FactTypeDefinition> builder)
    {
        builder.ToTable("fact_type_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ValueType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.DefaultConditionType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(32).IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(x => x.EventType).IsUnique();
        builder.HasIndex(x => x.IsActive);
    }
}
