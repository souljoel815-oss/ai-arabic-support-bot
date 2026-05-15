using EgyptTax.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class PrepaidExpenseConfiguration : IEntityTypeConfiguration<PrepaidExpense>
{
    public void Configure(EntityTypeBuilder<PrepaidExpense> b)
    {
        b.ToTable("prepaid_expenses", schema: "accounting");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(p => p.Description).HasColumnName("description").HasMaxLength(2000);
        b.ComplexProperty(p => p.TotalAmount,
            cp => cp.Property(x => x.Amount).HasColumnName("total_amount")
                .HasColumnType("decimal(19,2)").IsRequired());
        b.Property(p => p.ExpenseAccountCode).HasColumnName("expense_account_code")
            .HasMaxLength(16).IsUnicode(false).IsRequired();
        b.Property(p => p.StartMonth).HasColumnName("start_month")
            .HasColumnType("date").IsRequired();
        b.Property(p => p.PeriodCount).HasColumnName("period_count").IsRequired();
        b.Property(p => p.PeriodsRecognized).HasColumnName("periods_recognized").IsRequired();
        b.Property(p => p.LastRecognizedAtUtc).HasColumnName("last_recognized_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(p => p.CreatedByUserId).HasColumnName("created_by_user_id");
    }
}
