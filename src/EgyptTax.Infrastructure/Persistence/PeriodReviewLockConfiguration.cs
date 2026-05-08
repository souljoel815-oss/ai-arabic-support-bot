using EgyptTax.Domain.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class PeriodReviewLockConfiguration : IEntityTypeConfiguration<PeriodReviewLock>
{
    public void Configure(EntityTypeBuilder<PeriodReviewLock> b)
    {
        b.ToTable("period_review_locks", schema: "workflow");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(l => l.PeriodYear).HasColumnName("period_year").IsRequired();
        b.Property(l => l.PeriodMonth).HasColumnName("period_month").IsRequired();

        b.Property(l => l.LockedAtUtc).HasColumnName("locked_at_utc").HasColumnType("datetime2(3)").IsRequired();
        b.Property(l => l.LockedByUserId).HasColumnName("locked_by_user_id").IsRequired();
        b.Property(l => l.LockedNote).HasColumnName("locked_note").HasMaxLength(500);

        b.Property(l => l.ReleasedAtUtc).HasColumnName("released_at_utc").HasColumnType("datetime2(3)");
        b.Property(l => l.ReleasedByUserId).HasColumnName("released_by_user_id");

        b.Property(l => l.AccountantActionsDuringLock).HasColumnName("accountant_actions_during_lock").IsRequired();

        // Each (year, month) can have at most ONE active review lock.
        // Enforced via filtered unique index on the active rows
        // (released_at_utc IS NULL); historical released locks for
        // the same month are fine.
        b.HasIndex(l => new { l.PeriodYear, l.PeriodMonth })
            .IsUnique()
            .HasFilter("[released_at_utc] IS NULL")
            .HasDatabaseName("ux_period_review_locks_active_year_month");
    }
}
