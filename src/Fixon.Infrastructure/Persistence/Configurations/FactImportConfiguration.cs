using Fixon.Domain.Facts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class FactImportConfiguration : IEntityTypeConfiguration<FactImport>
{
    public void Configure(EntityTypeBuilder<FactImport> builder)
    {
        builder.ToTable("fact_imports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.RowsImported).IsRequired();
        builder.Property(x => x.ClaimsGenerated).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.CreatedByUserId).IsRequired();

        builder.HasIndex(x => new { x.CompanyId, x.CreatedAt });
    }
}
