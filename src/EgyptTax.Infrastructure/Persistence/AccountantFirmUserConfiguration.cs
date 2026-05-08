using EgyptTax.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class AccountantFirmUserConfiguration : IEntityTypeConfiguration<AccountantFirmUser>
{
    public void Configure(EntityTypeBuilder<AccountantFirmUser> b)
    {
        b.ToTable("accountant_firm_users", schema: "identity");

        // PK = UserId, also FK→User per data-model A4 — composition over User.
        b.HasKey(a => a.UserId);
        b.Property(a => a.UserId).HasColumnName("user_id").ValueGeneratedNever();
        b.HasOne<User>()
            .WithOne()
            .HasForeignKey<AccountantFirmUser>(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Property(a => a.FirmName).HasColumnName("firm_name").HasMaxLength(200).IsRequired();
        b.Property(a => a.FirmExternalIdentifier)
            .HasColumnName("firm_external_identifier")
            .HasMaxLength(200)
            .IsRequired();

        b.Property(a => a.InvitedAtUtc)
            .HasColumnName("invited_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();
        b.Property(a => a.InvitedByUserId).HasColumnName("invited_by_user_id").IsRequired();
        b.Property(a => a.AcceptedAtUtc)
            .HasColumnName("accepted_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(a => a.RevokedAtUtc)
            .HasColumnName("revoked_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(a => a.RevokedByUserId).HasColumnName("revoked_by_user_id");

        // Useful for the "is this user a firm user — and if so which
        // firm" lookup the audit-tagging behavior runs on every write.
        b.HasIndex(a => a.FirmExternalIdentifier).HasDatabaseName("ix_firm_users_external_id");
    }
}
