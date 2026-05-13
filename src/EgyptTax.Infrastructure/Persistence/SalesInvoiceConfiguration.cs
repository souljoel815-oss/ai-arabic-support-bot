using EgyptTax.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> b)
    {
        b.ToTable("sales_invoices", schema: "documents");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(s => s.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(s => s.DocumentDate)
            .HasColumnName("document_date")
            .HasColumnType("date")
            .IsRequired();
        b.Property(s => s.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        b.Property(s => s.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(32)
            .IsUnicode(false);
        b.Property(s => s.PostedAtUtc).HasColumnName("posted_at_utc").HasColumnType("datetime2(3)");
        b.Property(s => s.PostedByUserId).HasColumnName("posted_by_user_id");
        b.Property(s => s.CreatedByUserId).HasColumnName("created_by_user_id");
        b.HasIndex(s => s.CreatedByUserId).HasDatabaseName("ix_sales_invoices_created_by_user_id");
        b.Property(s => s.PostingMode)
            .HasColumnName("posting_mode")
            .HasConversion<string>()
            .HasMaxLength(32);

        // FR-013 — credit note → original invoice link + reason. Both
        // are nullable; non-null iff the row is a credit note.
        b.Property(s => s.CreditNoteOfInvoiceId).HasColumnName("credit_note_of_invoice_id");
        b.Property(s => s.CreditNoteReason).HasColumnName("credit_note_reason").HasMaxLength(2000);
        b.HasIndex(s => s.CreditNoteOfInvoiceId)
            .HasDatabaseName("ix_sales_invoices_credit_note_of_invoice_id")
            .HasFilter("[credit_note_of_invoice_id] IS NOT NULL");

        b.ComplexProperty(
            s => s.CustomerTaxProfileSnapshot,
            t =>
            {
                t.Property(x => x.ProfileType)
                    .HasColumnName("customer_tax_profile_snapshot_type")
                    .HasConversion<string>()
                    .HasMaxLength(32)
                    .IsRequired();
                t.Property(x => x.TinValue)
                    .HasColumnName("customer_tax_profile_snapshot_tin")
                    .HasMaxLength(9)
                    .IsUnicode(false);
                t.Property(x => x.VatExemption)
                    .HasColumnName("customer_tax_profile_snapshot_vat_exemption")
                    .IsRequired();
                t.Property(x => x.DefaultSalesVatCategoryId)
                    .HasColumnName("customer_tax_profile_snapshot_default_sales_vat_category_id");
            }
        );

        b.ComplexProperty(
            s => s.Subtotal,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("subtotal")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.ComplexProperty(
            s => s.InvoiceLevelDiscountAmount,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("invoice_level_discount_amount")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.Property(s => s.InvoiceLevelDiscountPercent)
            .HasColumnName("invoice_level_discount_percent")
            .HasColumnType("decimal(5,2)")
            .IsRequired();
        b.ComplexProperty(
            s => s.NetBeforeVat,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("net_before_vat")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.ComplexProperty(
            s => s.VatTotal,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("vat_total")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.ComplexProperty(
            s => s.GrandTotal,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("grand_total")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );

        b.HasMany(s => s.Lines)
            .WithOne()
            .HasForeignKey(l => l.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(SalesInvoice.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(s => s.DocumentNumber).HasDatabaseName("ix_sales_invoices_document_number");
        b.HasIndex(s => s.CustomerId).HasDatabaseName("ix_sales_invoices_customer_id");
    }
}
