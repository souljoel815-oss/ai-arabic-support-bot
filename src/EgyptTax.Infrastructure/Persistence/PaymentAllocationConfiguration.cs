using EgyptTax.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> b)
    {
        b.ToTable("payment_allocations", schema: "documents");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        // Two nullable parent FKs — the aggregate constructor enforces
        // the XOR (exactly one non-null) so the parent's collection
        // navigation owns it cleanly.
        b.Property(a => a.SupplierPaymentVoucherId).HasColumnName("supplier_payment_voucher_id");
        b.Property(a => a.CustomerReceiptVoucherId).HasColumnName("customer_receipt_voucher_id");

        b.Property(a => a.TargetDocumentId).HasColumnName("target_document_id").IsRequired();
        b.Property(a => a.TargetDocumentType)
            .HasColumnName("target_document_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        b.ComplexProperty(
            a => a.AllocatedAmount,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("allocated_amount")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );

        b.HasIndex(a => a.SupplierPaymentVoucherId)
            .HasDatabaseName("ix_payment_allocations_supplier_payment_voucher_id")
            .HasFilter("[supplier_payment_voucher_id] IS NOT NULL");
        b.HasIndex(a => a.CustomerReceiptVoucherId)
            .HasDatabaseName("ix_payment_allocations_customer_receipt_voucher_id")
            .HasFilter("[customer_receipt_voucher_id] IS NOT NULL");
        b.HasIndex(a => a.TargetDocumentId)
            .HasDatabaseName("ix_payment_allocations_target_document_id");
    }
}
