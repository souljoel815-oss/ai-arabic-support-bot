using EgyptTax.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", schema: "identity");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(254)
            .IsUnicode(false)
            .IsRequired();
        b.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ux_users_email");

        b.ComplexProperty(
            u => u.DisplayName,
            n =>
            {
                n.Property(x => x.Arabic)
                    .HasColumnName("display_name_ar")
                    .HasMaxLength(200)
                    .IsRequired();
                n.Property(x => x.English)
                    .HasColumnName("display_name_en")
                    .HasMaxLength(200)
                    .IsRequired();
            }
        );

        b.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(512)
            .IsUnicode(false)
            .IsRequired();
        b.Property(u => u.PasswordMustChange).HasColumnName("password_must_change");
        b.Property(u => u.PasswordChangedAtUtc)
            .HasColumnName("password_changed_at_utc")
            .HasColumnType("datetime2(3)");

        // MFA secret stored as binary (encrypted-at-rest envelope; the
        // value-converter wraps DPAPI on Windows and lands with T047).
        b.Property(u => u.MfaSecretEncrypted)
            .HasColumnName("mfa_secret_encrypted")
            .HasColumnType("varbinary(512)");
        b.Property(u => u.MfaEnrolledAtUtc)
            .HasColumnName("mfa_enrolled_at_utc")
            .HasColumnType("datetime2(3)");

        b.Property(u => u.PreferredLanguage)
            .HasColumnName("preferred_language")
            .HasConversion<string>()
            .HasMaxLength(2);
        b.Property(u => u.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);

        b.Property(u => u.CommissionRatePercent)
            .HasColumnName("commission_rate_percent")
            .HasColumnType("decimal(5,2)");

        // v5 B.1 — sales-team membership (nullable FK; no CASCADE
        // because we want soft-delete behaviour on teams via
        // SalesTeamStatus.Inactive instead).
        b.Property(u => u.SalesTeamId).HasColumnName("sales_team_id");
        b.HasIndex(u => u.SalesTeamId).HasDatabaseName("ix_users_sales_team_id");

        // v5 D.2.1 — default hourly billing rate for timesheets.
        b.Property(u => u.HourlyRateEgp)
            .HasColumnName("hourly_rate_egp")
            .HasColumnType("decimal(10,2)");
        // v5 — AI chat persistent-memory notes.
        b.Property(u => u.AiMemoryNotes)
            .HasColumnName("ai_memory_notes")
            .HasMaxLength(2000);

        b.Property(u => u.LastLoginAtUtc)
            .HasColumnName("last_login_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(u => u.LastLoginSucceeded).HasColumnName("last_login_succeeded");
        b.Property(u => u.FailedLoginCount).HasColumnName("failed_login_count");
        b.Property(u => u.LockoutUntilUtc)
            .HasColumnName("lockout_until_utc")
            .HasColumnType("datetime2(3)");

        b.HasMany(u => u.Roles)
            .WithMany()
            .UsingEntity(j => j.ToTable("user_roles", schema: "identity"));
    }
}
