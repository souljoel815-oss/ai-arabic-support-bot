using EgyptTax.Domain.Purchases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> b)
    {
        b.ToTable("purchase_invoices", schema: "documents");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(p => p.SupplierId).HasColumnName("supplier_id").IsRequired();
        b.Property(p => p.SupplierInvoiceNumber)
            .HasColumnName("supplier_invoice_number")
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();
        b.Property(p => p.DateReceived)
            .HasColumnName("date_received")
            .HasColumnType("date")
            .IsRequired();
        b.Property(p => p.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        b.Property(p => p.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(32)
            .IsUnicode(false);
        b.Property(p => p.PostedAtUtc).HasColumnName("posted_at_utc").HasColumnType("datetime2(3)");
        b.Property(p => p.PostedByUserId).HasColumnName("posted_by_user_id");

        // v5 E.3 — purchase credit notes.
        b.Property(p => p.CreditNoteOfPurchaseInvoiceId)
            .HasColumnName("credit_note_of_purchase_invoice_id");
        b.Property(p => p.CreditNoteReason)
            .HasColumnName("credit_note_reason")
            .HasMaxLength(2000);
        b.Property(p => p.PostingMode)
            .HasColumnName("posting_mode")
            .HasConversion<string>()
            .HasMaxLength(32);

        b.ComplexProperty(
            p => p.SupplierTaxProfileSnapshot,
            t =>
            {
                t.Property(x => x.ProfileType)
                    .HasColumnName("supplier_tax_profile_snapshot_type")
                    .HasConversion<string>()
                    .HasMaxLength(32)
                    .IsRequired();
                t.Property(x => x.TinValue)
                    .HasColumnName("supplier_tax_profile_snapshot_tin")
                    .HasMaxLength(9)
                    .IsUnicode(false);
                t.Property(x => x.ReverseChargeFlag)
                    .HasColumnName("supplier_tax_profile_snapshot_reverse_charge")
                    .IsRequired();
                t.Property(x => x.DefaultPurchaseVatCategoryId)
                    .HasColumnName(
                        "supplier_tax_profile_snapshot_default_purchase_vat_category_id"
                    );
            }
        );

        b.ComplexProperty(
            p => p.Subtotal,
            p2 =>
                p2.Property(x => x.Amount)
                    .HasColumnName("subtotal")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.ComplexProperty(
            p => p.VatTotal,
            p2 =>
                p2.Property(x => x.Amount)
                    .HasColumnName("vat_total")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.ComplexProperty(
            p => p.GrandTotal,
            p2 =>
                p2.Property(x => x.Amount)
                    .HasColumnName("grand_total")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );

        b.HasMany(p => p.Lines)
            .WithOne()
            .HasForeignKey(l => l.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(PurchaseInvoice.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(p => p.DocumentNumber).HasDatabaseName("ix_purchase_invoices_document_number");
        b.HasIndex(p => p.SupplierId).HasDatabaseName("ix_purchase_invoices_supplier_id");

        // T142 — covering index for the DuplicateSupplierInvoiceRule
        // fingerprint lookup `(supplier_id, supplier_invoice_number,
        // date_received, grand_total)`. The first two columns alone
        // are usually selective enough; the trailing columns turn
        // the index into a covering one for the dedup probe.
        b.HasIndex(p => new { p.SupplierId, p.SupplierInvoiceNumber })
            .HasDatabaseName("ix_purchase_invoices_supplier_dedup");
    }
}
