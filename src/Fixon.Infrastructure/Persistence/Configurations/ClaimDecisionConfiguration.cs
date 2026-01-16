using Fixon.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class ClaimDecisionConfiguration : IEntityTypeConfiguration<ClaimDecision>
{
    public void Configure(EntityTypeBuilder<ClaimDecision> builder)
    {
        builder.ToTable("claim_decisions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.ClaimId).IsRequired();
        builder.Property(x => x.DecisionType).HasConversion<int>().IsRequired();
        builder.Property(x => x.DecisionAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.DecidedByUserId).IsRequired();
        builder.Property(x => x.DecidedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(1000);

        builder.HasOne(x => x.Claim)
            .WithMany()
            .HasForeignKey(x => x.ClaimId);

        builder.HasOne(x => x.DecidedByUser)
            .WithMany()
            .HasForeignKey(x => x.DecidedByUserId);

        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => new { x.CompanyId, x.ClaimId, x.DecidedAt });
        builder.HasIndex(x => new { x.CompanyId, x.DecidedByUserId, x.DecidedAt });
    }
}

