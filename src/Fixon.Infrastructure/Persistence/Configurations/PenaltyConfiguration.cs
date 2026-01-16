using Fixon.Domain.Penalties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class PenaltyConfiguration : IEntityTypeConfiguration<Penalty>
{
    public void Configure(EntityTypeBuilder<Penalty> builder)
    {
        builder.ToTable("penalties");

        // PK = ClaimId (1:1)
        builder.HasKey(x => x.ClaimId);

        builder.Property(x => x.ClaimId).ValueGeneratedNever();
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.CalculatedAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.CalculatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.PenaltyJsonSnapshot).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ExplanationJson).HasColumnType("jsonb").IsRequired();

        builder.HasOne<Fixon.Domain.Claims.Claim>()
            .WithOne()
            .HasForeignKey<Penalty>(x => x.ClaimId);

        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => new { x.CompanyId, x.CalculatedAt });
    }
}

