using EgyptTax.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class DeferredRevenueConfiguration : IEntityTypeConfiguration<DeferredRevenue>
{
    public void Configure(EntityTypeBuilder<DeferredRevenue> b)
    {
        b.ToTable("deferred_revenues", schema: "accounting");
        b.HasKey(d => d.Id);
        b.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(d => d.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(d => d.Description).HasColumnName("description").HasMaxLength(2000);
        b.ComplexProperty(d => d.TotalAmount,
            cp => cp.Property(x => x.Amount).HasColumnName("total_amount")
                .HasColumnType("decimal(19,2)").IsRequired());
        b.Property(d => d.RevenueAccountCode).HasColumnName("revenue_account_code")
            .HasMaxLength(16).IsUnicode(false).IsRequired();
        b.Property(d => d.StartMonth).HasColumnName("start_month")
            .HasColumnType("date").IsRequired();
        b.Property(d => d.PeriodCount).HasColumnName("period_count").IsRequired();
        b.Property(d => d.PeriodsRecognized).HasColumnName("periods_recognized").IsRequired();
        b.Property(d => d.LastRecognizedAtUtc).HasColumnName("last_recognized_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(d => d.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(d => d.CreatedByUserId).HasColumnName("created_by_user_id");
    }
}
