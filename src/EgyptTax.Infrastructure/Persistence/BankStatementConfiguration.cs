using EgyptTax.Domain.Banking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class BankStatementConfiguration : IEntityTypeConfiguration<BankStatement>
{
    public void Configure(EntityTypeBuilder<BankStatement> b)
    {
        b.ToTable("bank_statements", schema: "documents");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(s => s.CashAccountId).HasColumnName("cash_account_id").IsRequired();
        b.Property(s => s.PeriodStart).HasColumnName("period_start").HasColumnType("date").IsRequired();
        b.Property(s => s.PeriodEnd).HasColumnName("period_end").HasColumnType("date").IsRequired();
        b.Property(s => s.SourceFileName).HasColumnName("source_file_name").HasMaxLength(256);
        b.Property(s => s.ImportedAtUtc).HasColumnName("imported_at_utc").HasColumnType("datetime2(3)").IsRequired();

        b.ComplexProperty(s => s.OpeningBalance, p =>
            p.Property(x => x.Amount).HasColumnName("opening_balance").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(s => s.ClosingBalance, p =>
            p.Property(x => x.Amount).HasColumnName("closing_balance").HasColumnType("decimal(19,2)").IsRequired());

        b.HasMany(s => s.Lines)
            .WithOne()
            .HasForeignKey(l => l.StatementId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(s => new { s.CashAccountId, s.PeriodStart })
            .HasDatabaseName("ix_bank_statements_account_period");
    }
}

internal sealed class BankStatementLineConfiguration : IEntityTypeConfiguration<BankStatementLine>
{
    public void Configure(EntityTypeBuilder<BankStatementLine> b)
    {
        b.ToTable("bank_statement_lines", schema: "documents");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(l => l.StatementId).HasColumnName("statement_id").IsRequired();
        b.Property(l => l.TransactionDate).HasColumnName("transaction_date").HasColumnType("date").IsRequired();
        b.Property(l => l.Description).HasColumnName("description").HasMaxLength(512).IsRequired();
        b.Property(l => l.BankReference).HasColumnName("bank_reference").HasMaxLength(64).IsUnicode(false);

        b.ComplexProperty(l => l.Debit, p =>
            p.Property(x => x.Amount).HasColumnName("debit").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(l => l.Credit, p =>
            p.Property(x => x.Amount).HasColumnName("credit").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(l => l.RunningBalance, p =>
            p.Property(x => x.Amount).HasColumnName("running_balance").HasColumnType("decimal(19,2)").IsRequired());

        b.Property(l => l.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();
        b.Property(l => l.MatchedSupplierPaymentVoucherId).HasColumnName("matched_supplier_payment_voucher_id");
        b.Property(l => l.MatchedCustomerReceiptVoucherId).HasColumnName("matched_customer_receipt_voucher_id");
        b.Property(l => l.MatchedAtUtc).HasColumnName("matched_at_utc").HasColumnType("datetime2(3)");
        b.Property(l => l.MatchedByUserId).HasColumnName("matched_by_user_id");

        // P3.4 — auto-match scorer columns.
        b.Property(l => l.MatchConfidenceScore).HasColumnName("match_confidence_score");
        b.Property(l => l.SuggestedSupplierPaymentVoucherId).HasColumnName("suggested_supplier_payment_voucher_id");
        b.Property(l => l.SuggestedCustomerReceiptVoucherId).HasColumnName("suggested_customer_receipt_voucher_id");
        b.Property(l => l.SuggestedAtUtc).HasColumnName("suggested_at_utc").HasColumnType("datetime2(3)");

        // Drives the unmatched-queue page (P3.5).
        b.HasIndex(l => new { l.Status, l.TransactionDate })
            .HasDatabaseName("ix_bank_statement_lines_status_date");
    }
}
