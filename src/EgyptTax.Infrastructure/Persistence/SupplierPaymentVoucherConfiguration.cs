using EgyptTax.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class SupplierPaymentVoucherConfiguration
    : IEntityTypeConfiguration<SupplierPaymentVoucher>
{
    public void Configure(EntityTypeBuilder<SupplierPaymentVoucher> b)
    {
        b.ToTable("supplier_payment_vouchers", schema: "documents");
        b.HasKey(v => v.Id);
        b.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(v => v.SupplierId).HasColumnName("supplier_id").IsRequired();
        b.Property(v => v.CashAccountId).HasColumnName("cash_account_id");
        b.Property(v => v.PaymentDate)
            .HasColumnName("payment_date")
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
            v => v.GrossPaymentAmount,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("gross_payment_amount")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.ComplexProperty(
            v => v.WhtPayableAmount,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("wht_payable_amount")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.ComplexProperty(
            v => v.NetCashPaid,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("net_cash_paid")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.Property(v => v.GeneratedWhtCertificateId).HasColumnName("generated_wht_certificate_id");

        b.HasMany(v => v.Allocations)
            .WithOne()
            .HasForeignKey(a => a.SupplierPaymentVoucherId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(SupplierPaymentVoucher.Allocations))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(v => v.SupplierId).HasDatabaseName("ix_supplier_payment_vouchers_supplier_id");
        b.HasIndex(v => v.PaymentDate).HasDatabaseName("ix_supplier_payment_vouchers_payment_date");
        b.HasIndex(v => v.DocumentNumber)
            .HasDatabaseName("ix_supplier_payment_vouchers_document_number")
            .HasFilter("[document_number] IS NOT NULL");
    }
}
