using EgyptTax.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> b)
    {
        b.ToTable("sales_orders", schema: "documents");
        b.HasKey(o => o.Id);
        b.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(o => o.OrderNumber).HasColumnName("order_number").HasMaxLength(32).IsUnicode(false).IsRequired();
        b.Property(o => o.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(o => o.OrderDate).HasColumnName("order_date").HasColumnType("date").IsRequired();
        b.Property(o => o.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(o => o.Note).HasColumnName("note").HasMaxLength(1000);
        b.Property(o => o.CreatedByUserId).HasColumnName("created_by_user_id");
        b.Property(o => o.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime2(3)").IsRequired();
        b.Property(o => o.ConfirmedAtUtc).HasColumnName("confirmed_at_utc").HasColumnType("datetime2(3)");
        b.Property(o => o.ConvertedAtUtc).HasColumnName("converted_at_utc").HasColumnType("datetime2(3)");
        b.Property(o => o.ConvertedToInvoiceId).HasColumnName("converted_to_invoice_id");

        b.ComplexProperty(o => o.Subtotal,
            p => p.Property(x => x.Amount).HasColumnName("subtotal").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(o => o.VatTotal,
            p => p.Property(x => x.Amount).HasColumnName("vat_total").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(o => o.GrandTotal,
            p => p.Property(x => x.Amount).HasColumnName("grand_total").HasColumnType("decimal(19,2)").IsRequired());

        b.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey(l => l.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(SalesOrder.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(o => o.CreatedByUserId).HasDatabaseName("ix_sales_orders_created_by_user_id");
        b.HasIndex(o => o.CustomerId).HasDatabaseName("ix_sales_orders_customer_id");
        b.HasIndex(o => o.State).HasDatabaseName("ix_sales_orders_state");
    }
}

internal sealed class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
{
    public void Configure(EntityTypeBuilder<SalesOrderLine> b)
    {
        b.ToTable("sales_order_lines", schema: "documents");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(l => l.SalesOrderId).HasColumnName("sales_order_id").IsRequired();
        b.Property(l => l.ItemId).HasColumnName("item_id").IsRequired();
        b.Property(l => l.Quantity).HasColumnName("quantity").HasColumnType("decimal(19,3)").IsRequired();
        b.Property(l => l.VatCategoryId).HasColumnName("vat_category_id").IsRequired();
        b.Property(l => l.VatRatePercent).HasColumnName("vat_rate_percent").HasColumnType("decimal(5,2)").IsRequired();

        b.ComplexProperty(l => l.UnitPrice,
            p => p.Property(x => x.Amount).HasColumnName("unit_price").HasColumnType("decimal(19,2)").IsRequired());
    }
}
