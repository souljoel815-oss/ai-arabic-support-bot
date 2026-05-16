using EgyptTax.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class CustomerReceiptVoucherConfiguration
    : IEntityTypeConfiguration<CustomerReceiptVoucher>
{
    public void Configure(EntityTypeBuilder<CustomerReceiptVoucher> b)
    {
        b.ToTable("customer_receipt_vouchers", schema: "documents");
        b.HasKey(v => v.Id);
        b.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(v => v.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(v => v.CashAccountId).HasColumnName("cash_account_id");
        b.Property(v => v.ReceiptDate)
            .HasColumnName("receipt_date")
            .HasColumnType("date")
            .IsRequired();
        b.Property(v => v.PaymentMethod)
            .HasColumnName("payment_method")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(v => v.PaymentReference)
            .HasColumnName("payment_reference")
            .HasMaxLength(64)
            .IsRequired();
        b.Property(v => v.Note).HasColumnName("note").HasMaxLength(500);
        b.Property(v => v.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(v => v.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(32)
            .IsUnicode(false);
        b.Property(v => v.PostedAtUtc).HasColumnName("posted_at_utc").HasColumnType("datetime2(3)");
        b.Property(v => v.PostedByUserId).HasColumnName("posted_by_user_id");

        b.ComplexProperty(
            v => v.GrossReceiptAmount,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("gross_receipt_amount")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.ComplexProperty(
            v => v.WhtReceivableAmount,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("wht_receivable_amount")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        // v5 E.8 — early-payment discount the customer took.
        b.ComplexProperty(
            v => v.DiscountTakenAmount,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("discount_taken_amount")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.ComplexProperty(
            v => v.NetCashReceived,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("net_cash_received")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.Property(v => v.CustomerWhtCertificateId).HasColumnName("customer_wht_certificate_id");

        b.HasMany(v => v.Allocations)
            .WithOne()
            .HasForeignKey(a => a.CustomerReceiptVoucherId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(CustomerReceiptVoucher.Allocations))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(v => v.CustomerId).HasDatabaseName("ix_customer_receipt_vouchers_customer_id");
        b.HasIndex(v => v.ReceiptDate).HasDatabaseName("ix_customer_receipt_vouchers_receipt_date");
        b.HasIndex(v => v.DocumentNumber)
            .HasDatabaseName("ix_customer_receipt_vouchers_document_number")
            .HasFilter("[document_number] IS NOT NULL");
    }
}
