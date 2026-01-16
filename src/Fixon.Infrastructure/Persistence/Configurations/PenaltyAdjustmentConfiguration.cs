using Fixon.Domain.Penalties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class PenaltyAdjustmentConfiguration : IEntityTypeConfiguration<PenaltyAdjustment>
{
    public void Configure(EntityTypeBuilder<PenaltyAdjustment> builder)
    {
        builder.ToTable("penalty_adjustments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.ClaimId).IsRequired();

        builder.Property(x => x.OriginalPenaltyAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.NewPenaltyAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.AdjustedByUserId).IsRequired();
        builder.Property(x => x.AdjustedAt).HasColumnType("timestamp with time zone").IsRequired();

        builder.HasOne<Fixon.Domain.Claims.Claim>()
            .WithMany()
            .HasForeignKey(x => x.ClaimId);

        builder.HasOne<Fixon.Domain.Users.User>()
            .WithMany()
            .HasForeignKey(x => x.AdjustedByUserId);

        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => new { x.CompanyId, x.ClaimId, x.AdjustedAt });
    }
}

