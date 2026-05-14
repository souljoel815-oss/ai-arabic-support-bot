using EgyptTax.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> b)
    {
        b.ToTable("api_keys", schema: "settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        b.Property(x => x.DisplayPrefix).HasColumnName("display_prefix")
            .HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(x => x.KeyHash).HasColumnName("key_hash")
            .HasMaxLength(64).IsUnicode(false).IsRequired();
        // Hash is the lookup key for auth; needs an index for the
        // per-request lookup to stay O(log n).
        b.HasIndex(x => x.KeyHash).IsUnique().HasDatabaseName("ux_api_keys_key_hash");

        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        b.Property(x => x.LastUsedAtUtc).HasColumnName("last_used_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(x => x.Revoked).HasColumnName("revoked").IsRequired();
        b.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc")
            .HasColumnType("datetime2(3)");
    }
}
