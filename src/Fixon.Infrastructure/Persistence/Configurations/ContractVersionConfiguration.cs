using Fixon.Domain.Contracts;
using Fixon.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class ContractVersionConfiguration : IEntityTypeConfiguration<ContractVersion>
{
    public void Configure(EntityTypeBuilder<ContractVersion> builder)
    {
        builder.ToTable("contract_versions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.ContractId).IsRequired();
        builder.Property(x => x.VersionNumber).IsRequired();
        builder.Property(x => x.EffectiveFrom).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.EffectiveTo).HasColumnType("timestamp with time zone");
        builder.Property(x => x.PdfFilePath).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.CreatedByUserId).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.SignedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.SlaRulesSnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.PenaltyRulesSnapshotJson).HasColumnType("jsonb").IsRequired();

        builder.HasOne(x => x.Contract)
            .WithMany(c => c.Versions)
            .HasForeignKey(x => x.ContractId);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId);

        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => new { x.ContractId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.ContractId });
        builder.HasIndex(x => new { x.CompanyId, x.ContractId, x.Status });
    }
}


