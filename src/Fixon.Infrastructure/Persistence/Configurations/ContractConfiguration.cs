using Fixon.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("contracts");

        builder.HasKey(x => x.Id);

        // PostgreSQL system column xmin as optimistic concurrency token (shadow property).
        // IMPORTANT: xmin is a system column; we must NOT create it via migrations.
        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.CounterpartyId)
            .HasColumnName("counterparty_id")
            .IsRequired(false);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.CurrentVersionId);

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId);

        builder.HasOne(x => x.Counterparty)
            .WithMany()
            .HasForeignKey(x => x.CounterpartyId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Versions)
            .WithOne(v => v.Contract)
            .HasForeignKey(v => v.ContractId);

        builder.HasMany(x => x.SlaRules)
            .WithOne(r => r.Contract)
            .HasForeignKey(r => r.ContractId);

        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => new { x.CompanyId, x.CounterpartyId });
        builder.HasIndex(x => new { x.CompanyId, x.Status });
    }
}


