using Fixon.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class ImportRowConfiguration : IEntityTypeConfiguration<ImportRow>
{
    public void Configure(EntityTypeBuilder<ImportRow> builder)
    {
        builder.ToTable("import_rows");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.ImportBatchId).IsRequired();
        builder.Property(x => x.RowNumber).IsRequired();
        builder.Property(x => x.RawPayload).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ValidationStatus).HasConversion<int>().IsRequired();
        builder.Property(x => x.ErrorCode).HasMaxLength(100);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(x => x.ImportBatch)
            .WithMany()
            .HasForeignKey(x => x.ImportBatchId);

        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => new { x.CompanyId, x.ImportBatchId, x.RowNumber }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.ImportBatchId, x.ValidationStatus });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_import_rows_validation_status", "\"ValidationStatus\" in (1,2,3,4)");
        });
    }
}

