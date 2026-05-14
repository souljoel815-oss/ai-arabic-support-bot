using EgyptTax.Domain.Purchases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class PurchaseInvoiceLineConfiguration
    : IEntityTypeConfiguration<PurchaseInvoiceLine>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceLine> b)
    {
        b.ToTable("purchase_invoice_lines", schema: "documents");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(l => l.PurchaseInvoiceId).HasColumnName("purchase_invoice_id").IsRequired();

        // C2 invariant: ItemId XOR ExpenseCategoryId. Both columns
        // are nullable; the domain enforces XOR at construction. A
        // CHECK constraint at the DB level would catch raw-SQL
        // tampering — added here.
        b.Property(l => l.ItemId).HasColumnName("item_id");
        b.Property(l => l.ExpenseCategoryId).HasColumnName("expense_category_id");
        b.ToTable(t =>
            t.HasCheckConstraint(
                "ck_purchase_invoice_lines_item_xor_expense",
                "([item_id] IS NOT NULL AND [expense_category_id] IS NULL) OR ([item_id] IS NULL AND [expense_category_id] IS NOT NULL)"
            )
        );

        b.Property(l => l.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("decimal(18,4)")
            .IsRequired();
        b.ComplexProperty(
            l => l.UnitPrice,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("unit_price")
                    .HasColumnType("decimal(19,4)")
                    .IsRequired()
        );
        b.Property(l => l.VatCategoryId).HasColumnName("vat_category_id").IsRequired();
        b.Property(l => l.VatRatePercent)
            .HasColumnName("vat_rate_percent")
            .HasColumnType("decimal(5,2)")
            .IsRequired();
        b.Property(l => l.DeductibleFlag).HasColumnName("deductible_flag").IsRequired();

        // v4 C.2 — optional per-line cost-center tag (nullable FK).
        b.Property(l => l.CostCenterId).HasColumnName("cost_center_id");
        b.HasIndex(l => l.CostCenterId)
            .HasDatabaseName("ix_purchase_invoice_lines_cost_center");

        b.ComplexProperty(
            l => l.LineSubtotal,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("line_subtotal")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.ComplexProperty(
            l => l.LineVat,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("line_vat")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.ComplexProperty(
            l => l.LineTotal,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("line_total")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
    }
}
