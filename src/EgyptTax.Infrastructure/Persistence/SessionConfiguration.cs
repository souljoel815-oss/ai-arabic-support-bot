using EgyptTax.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> b)
    {
        b.ToTable("sessions", schema: "identity");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
        b.HasIndex(s => s.UserId).HasDatabaseName("ix_sessions_user_id");

        b.Property(s => s.IssuedAtUtc).HasColumnName("issued_at_utc").HasColumnType("datetime2(3)").IsRequired();
        b.Property(s => s.LastActivityAtUtc).HasColumnName("last_activity_at_utc").HasColumnType("datetime2(3)").IsRequired();
        b.Property(s => s.AbsoluteExpiresAtUtc).HasColumnName("absolute_expires_at_utc").HasColumnType("datetime2(3)").IsRequired();

        b.Property(s => s.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("datetime2(3)");
        b.Property(s => s.RevocationReason)
            .HasColumnName("revocation_reason")
            .HasConversion<string>()
            .HasMaxLength(40);

        b.Property(s => s.IpAddress).HasColumnName("ip_address").IsUnicode(false).HasMaxLength(45);
        b.Property(s => s.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
    }
}
