using EgyptTax.Domain.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> b)
    {
        b.ToTable("expenses", schema: "documents");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(e => e.DocumentDate)
            .HasColumnName("document_date")
            .HasColumnType("date")
            .IsRequired();
        b.Property(e => e.CategoryId).HasColumnName("category_id").IsRequired();
        b.Property(e => e.DeductibleFlag).HasColumnName("deductible_flag").IsRequired();
        b.Property(e => e.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(e => e.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(32)
            .IsUnicode(false);
        b.Property(e => e.PostedAtUtc).HasColumnName("posted_at_utc").HasColumnType("datetime2(3)");
        b.Property(e => e.PostedByUserId).HasColumnName("posted_by_user_id");
        b.Property(e => e.PostingMode)
            .HasColumnName("posting_mode")
            .HasConversion<string>()
            .HasMaxLength(32);

        b.ComplexProperty(
            e => e.Amount,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("amount")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );

        b.ComplexProperty(
            e => e.Description,
            n =>
            {
                n.Property(x => x.Arabic)
                    .HasColumnName("description_ar")
                    .HasMaxLength(2000)
                    .IsRequired();
                n.Property(x => x.English)
                    .HasColumnName("description_en")
                    .HasMaxLength(2000)
                    .IsRequired();
            }
        );

        b.HasIndex(e => e.DocumentNumber).HasDatabaseName("ix_expenses_document_number");
        b.HasIndex(e => e.CategoryId).HasDatabaseName("ix_expenses_category_id");
        b.HasIndex(e => e.DocumentDate).HasDatabaseName("ix_expenses_document_date");

        // v3 §11 #3 — cost-center tag (nullable FK).
        b.Property(e => e.CostCenterId).HasColumnName("cost_center_id");
        b.HasIndex(e => e.CostCenterId).HasDatabaseName("ix_expenses_cost_center_id");

        // v5 B.4 — bundle FK (nullable; null = standalone expense).
        b.Property(e => e.ExpenseReportId).HasColumnName("expense_report_id");
        b.HasIndex(e => e.ExpenseReportId).HasDatabaseName("ix_expenses_expense_report_id");
    }
}
