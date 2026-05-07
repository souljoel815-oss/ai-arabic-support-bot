using EgyptTax.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> b)
    {
        b.ToTable("password_reset_tokens", schema: "identity");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        b.HasIndex(t => t.UserId).HasDatabaseName("ix_password_reset_tokens_user_id");

        b.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .HasColumnType("varbinary(32)")
            .IsRequired();
        b.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("ux_password_reset_tokens_hash");

        b.Property(t => t.IssuedAtUtc).HasColumnName("issued_at_utc").HasColumnType("datetime2(3)").IsRequired();
        b.Property(t => t.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("datetime2(3)").IsRequired();
        b.Property(t => t.RedeemedAtUtc).HasColumnName("redeemed_at_utc").HasColumnType("datetime2(3)");
        b.Property(t => t.IssuedByAdminUserId).HasColumnName("issued_by_admin_user_id").IsRequired();
    }
}
