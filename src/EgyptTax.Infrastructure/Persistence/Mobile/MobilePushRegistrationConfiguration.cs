using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence.Mobile;

/// <summary>
/// EF Core configuration for <see cref="MobilePushRegistration"/>.
/// Auto-discovered by <c>AppDbContext.OnModelCreating</c> via
/// <c>ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)</c>.
///
/// Per data-model.md §5:
///   - PK on Id.
///   - Index on UserId (every notifier queries by user).
///   - Composite UNIQUE on (UserId, DeviceId) WHERE RevokedAtUtc IS NULL
///     so re-registering after a logout reuses the row instead of
///     piling up zombie entries.
/// </summary>
internal sealed class MobilePushRegistrationConfiguration : IEntityTypeConfiguration<MobilePushRegistration>
{
    public void Configure(EntityTypeBuilder<MobilePushRegistration> b)
    {
        b.ToTable("mobile_push_registrations");

        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id");

        b.Property(r => r.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        b.Property(r => r.DeviceId)
            .HasColumnName("device_id")
            .HasMaxLength(64)
            .IsRequired();

        b.Property(r => r.FcmToken)
            .HasColumnName("fcm_token")
            .HasMaxLength(512)
            .IsRequired();

        b.Property(r => r.Platform)
            .HasColumnName("platform")
            .HasMaxLength(16)
            .IsRequired();

        b.Property(r => r.AppVersion)
            .HasColumnName("app_version")
            .HasMaxLength(32)
            .IsRequired();

        b.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(r => r.LastSeenAtUtc).HasColumnName("last_seen_at_utc");
        b.Property(r => r.RevokedAtUtc).HasColumnName("revoked_at_utc");

        b.HasIndex(r => r.UserId)
            .HasDatabaseName("ix_mobile_push_registrations_user_id");

        // Filtered unique on (user, device) while still active. The
        // filter expression is the standard EF Core syntax for
        // SQL Server; SQLite ignores it and treats the index as
        // unconditionally unique, which is OK because in practice a
        // single device wouldn't have two simultaneously-active
        // registrations for the same user.
        b.HasIndex(r => new { r.UserId, r.DeviceId })
            .IsUnique()
            .HasFilter("[revoked_at_utc] IS NULL")
            .HasDatabaseName("ux_mobile_push_registrations_user_device");
    }
}
