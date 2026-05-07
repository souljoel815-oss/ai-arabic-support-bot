using EgyptTax.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class SalesInvoiceLineConfiguration : IEntityTypeConfiguration<SalesInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceLine> b)
    {
        b.ToTable("sales_invoice_lines", schema: "documents");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(l => l.SalesInvoiceId).HasColumnName("sales_invoice_id").IsRequired();
        b.Property(l => l.ItemId).HasColumnName("item_id").IsRequired();

        b.Property(l => l.Quantity).HasColumnName("quantity").HasColumnType("decimal(19,4)").IsRequired();

        b.ComplexProperty(l => l.UnitPrice, p => p.Property(x => x.Amount).HasColumnName("unit_price").HasColumnType("decimal(19,4)").IsRequired());
        b.Property(l => l.VatCategoryId).HasColumnName("vat_category_id").IsRequired();
        b.Property(l => l.VatRatePercent).HasColumnName("vat_rate_percent").HasColumnType("decimal(5,2)").IsRequired();

        b.ComplexProperty(l => l.LineSubtotal,             p => p.Property(x => x.Amount).HasColumnName("line_subtotal").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(l => l.LineApportionedDiscount,  p => p.Property(x => x.Amount).HasColumnName("line_apportioned_discount").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(l => l.LineNetSubtotal,          p => p.Property(x => x.Amount).HasColumnName("line_net_subtotal").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(l => l.LineVat,                  p => p.Property(x => x.Amount).HasColumnName("line_vat").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(l => l.LineTotal,                p => p.Property(x => x.Amount).HasColumnName("line_total").HasColumnType("decimal(19,2)").IsRequired());
    }
}
