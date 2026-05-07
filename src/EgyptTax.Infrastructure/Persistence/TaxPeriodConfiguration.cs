using EgyptTax.Domain.Periods;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class TaxPeriodConfiguration : IEntityTypeConfiguration<TaxPeriod>
{
    public void Configure(EntityTypeBuilder<TaxPeriod> b)
    {
        b.ToTable("tax_periods", schema: "workflow");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(p => p.PeriodKind).HasColumnName("period_kind")
            .HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(p => p.Year).HasColumnName("year").IsRequired();
        b.Property(p => p.MonthOrQuarter).HasColumnName("month_or_quarter").IsRequired();
        b.Property(p => p.Status).HasColumnName("status")
            .HasConversion<string>().HasMaxLength(16).IsRequired();

        b.Property(p => p.LockedAtUtc).HasColumnName("locked_at_utc").HasColumnType("datetime2(3)");
        b.Property(p => p.LockedByUserId).HasColumnName("locked_by_user_id");
        b.Property(p => p.LockedReason).HasColumnName("locked_reason").HasMaxLength(500);
        b.Property(p => p.ReopenedAtUtc).HasColumnName("reopened_at_utc").HasColumnType("datetime2(3)");
        b.Property(p => p.ReopenedByUserId).HasColumnName("reopened_by_user_id");
        b.Property(p => p.ReopenedReason).HasColumnName("reopened_reason").HasMaxLength(500);

        // Unique on the natural key (kind, year, month_or_quarter)
        // so a "find or create" lookup is a single seek + the
        // application can Upsert via this index when locking a period
        // that has never been touched before.
        b.HasIndex(p => new { p.PeriodKind, p.Year, p.MonthOrQuarter })
            .IsUnique()
            .HasDatabaseName("ux_tax_periods_kind_year_month");
    }
}
