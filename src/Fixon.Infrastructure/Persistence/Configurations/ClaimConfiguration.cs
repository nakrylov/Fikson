using Fixon.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("claims");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.ContractId).IsRequired();
        builder.Property(x => x.ContractVersionId).IsRequired();
        builder.Property(x => x.SlaRuleVersionId).IsRequired();
        builder.Property(x => x.SlaViolationId).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.OpenedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ClosedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedByUserId).IsRequired();

        builder.HasOne(x => x.Contract)
            .WithMany()
            .HasForeignKey(x => x.ContractId);

        builder.HasOne(x => x.ContractVersion)
            .WithMany(v => v.Claims)
            .HasForeignKey(x => x.ContractVersionId);

        builder.HasOne(x => x.SlaRuleVersion)
            .WithMany(v => v.Claims)
            .HasForeignKey(x => x.SlaRuleVersionId);

        builder.HasOne(x => x.SlaViolation)
            .WithMany()
            .HasForeignKey(x => x.SlaViolationId);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId);

        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => new { x.CompanyId, x.OpenedAt });
        builder.HasIndex(x => new { x.CompanyId, x.Status });
        builder.HasIndex(x => new { x.CompanyId, x.ContractVersionId });
        builder.HasIndex(x => new { x.CompanyId, x.ContractId });
        builder.HasIndex(x => new { x.CompanyId, x.SlaRuleVersionId });
        builder.HasIndex(x => new { x.CompanyId, x.SlaViolationId });
        builder.HasIndex(x => x.SlaViolationId).IsUnique();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_claims_status", "\"Status\" in (1,2,3,4,5,6,7,8)");
        });
    }
}


