using Fixon.Domain.Facts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class FactConfiguration : IEntityTypeConfiguration<Fact>
{
    public void Configure(EntityTypeBuilder<Fact> builder)
    {
        builder.ToTable("facts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.ContractId);
        builder.Property(x => x.ExternalReference).HasMaxLength(200);
        builder.Property(x => x.FactType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AttributesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ImportBatchId);
        builder.Property(x => x.PayloadHash).HasMaxLength(128);

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId);

        builder.HasOne(x => x.Contract)
            .WithMany()
            .HasForeignKey(x => x.ContractId);

        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => new { x.CompanyId, x.OccurredAt });
        builder.HasIndex(x => new { x.CompanyId, x.ContractId, x.OccurredAt });
        builder.HasIndex(x => new { x.CompanyId, x.FactType, x.OccurredAt });
        builder.HasIndex(x => new { x.CompanyId, x.ImportBatchId, x.CreatedAt });

        // Idempotency key (v2): CompanyId + ExternalReference + OccurredAt (only when ExternalReference is not null).
        builder.HasIndex(x => new { x.CompanyId, x.ExternalReference, x.OccurredAt })
            .IsUnique()
            .HasFilter("\"ExternalReference\" is not null");
    }
}


