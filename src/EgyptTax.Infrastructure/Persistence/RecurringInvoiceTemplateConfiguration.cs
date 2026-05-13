using EgyptTax.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class RecurringInvoiceTemplateConfiguration : IEntityTypeConfiguration<RecurringInvoiceTemplate>
{
    public void Configure(EntityTypeBuilder<RecurringInvoiceTemplate> b)
    {
        b.ToTable("recurring_invoice_templates", schema: "documents");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(t => t.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(t => t.Interval)
            .HasColumnName("interval")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(t => t.NextRunDate).HasColumnName("next_run_date").HasColumnType("date").IsRequired();
        b.Property(t => t.EndDate).HasColumnName("end_date").HasColumnType("date");
        b.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(t => t.Note).HasColumnName("note").HasMaxLength(500);
        b.Property(t => t.CreatedByUserId).HasColumnName("created_by_user_id");
        b.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime2(3)").IsRequired();
        b.Property(t => t.LastGeneratedDate).HasColumnName("last_generated_date").HasColumnType("date");
        b.Property(t => t.InvoicesGeneratedCount).HasColumnName("invoices_generated_count").IsRequired();

        b.HasMany(t => t.Lines)
            .WithOne()
            .HasForeignKey(l => l.RecurringInvoiceTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(RecurringInvoiceTemplate.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(t => new { t.IsActive, t.NextRunDate })
            .HasDatabaseName("ix_recurring_invoice_templates_active_next_run");
    }
}

internal sealed class RecurringInvoiceTemplateLineConfiguration : IEntityTypeConfiguration<RecurringInvoiceTemplateLine>
{
    public void Configure(EntityTypeBuilder<RecurringInvoiceTemplateLine> b)
    {
        b.ToTable("recurring_invoice_template_lines", schema: "documents");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(l => l.RecurringInvoiceTemplateId).HasColumnName("recurring_invoice_template_id").IsRequired();
        b.Property(l => l.ItemId).HasColumnName("item_id").IsRequired();
        b.Property(l => l.Quantity).HasColumnName("quantity").HasColumnType("decimal(19,3)").IsRequired();
        b.Property(l => l.VatCategoryId).HasColumnName("vat_category_id").IsRequired();
        b.Property(l => l.VatRatePercent).HasColumnName("vat_rate_percent").HasColumnType("decimal(5,2)").IsRequired();
        b.ComplexProperty(l => l.UnitPrice,
            p => p.Property(x => x.Amount).HasColumnName("unit_price").HasColumnType("decimal(19,2)").IsRequired());
    }
}
