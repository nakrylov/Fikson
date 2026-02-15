using Fixon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fixon.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRequestConfiguration : IEntityTypeConfiguration<IdempotencyRequest>
{
    public void Configure(EntityTypeBuilder<IdempotencyRequest> builder)
    {
        builder.ToTable("idempotency_requests");

        builder.HasKey(x => new { x.TenantId, x.Key, x.Endpoint });

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Endpoint).HasMaxLength(300).IsRequired();

        builder.Property(x => x.ResponseStatusCode).IsRequired();
        builder.Property(x => x.ResponseBody).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Key, x.Endpoint }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.CreatedAt);
    }
}

