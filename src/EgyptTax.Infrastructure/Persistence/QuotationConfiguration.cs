using EgyptTax.Domain.Quotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> b)
    {
        b.ToTable("quotations", schema: "documents");
        b.HasKey(q => q.Id);
        b.Property(q => q.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(q => q.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(q => q.DocumentDate)
            .HasColumnName("document_date").HasColumnType("date").IsRequired();
        b.Property(q => q.ValidUntilDate)
            .HasColumnName("valid_until_date").HasColumnType("date").IsRequired();

        b.Property(q => q.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        b.Property(q => q.QuotationNumber)
            .HasColumnName("quotation_number").HasMaxLength(32).IsUnicode(false);
        b.Property(q => q.SentAtUtc).HasColumnName("sent_at_utc").HasColumnType("datetime2(3)");
        b.Property(q => q.AcceptedAtUtc).HasColumnName("accepted_at_utc").HasColumnType("datetime2(3)");
        b.Property(q => q.RejectedAtUtc).HasColumnName("rejected_at_utc").HasColumnType("datetime2(3)");
        b.Property(q => q.ExpiredAtUtc).HasColumnName("expired_at_utc").HasColumnType("datetime2(3)");

        b.Property(q => q.ConvertedToInvoiceId).HasColumnName("converted_to_invoice_id");
        b.Property(q => q.ConvertedAtUtc).HasColumnName("converted_at_utc").HasColumnType("datetime2(3)");

        b.Property(q => q.CreatedByUserId).HasColumnName("created_by_user_id");
        b.Property(q => q.Notes).HasColumnName("notes").HasMaxLength(2000);

        b.ComplexProperty(q => q.CustomerTaxProfileSnapshot, t =>
        {
            t.Property(x => x.ProfileType)
                .HasColumnName("customer_tax_profile_snapshot_type")
                .HasConversion<string>().HasMaxLength(32).IsRequired();
            t.Property(x => x.TinValue)
                .HasColumnName("customer_tax_profile_snapshot_tin")
                .HasMaxLength(9).IsUnicode(false);
            t.Property(x => x.VatExemption)
                .HasColumnName("customer_tax_profile_snapshot_vat_exemption").IsRequired();
            t.Property(x => x.DefaultSalesVatCategoryId)
                .HasColumnName("customer_tax_profile_snapshot_default_sales_vat_category_id");
        });

        b.ComplexProperty(q => q.Subtotal,
            p => p.Property(x => x.Amount).HasColumnName("subtotal")
                .HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(q => q.NetBeforeVat,
            p => p.Property(x => x.Amount).HasColumnName("net_before_vat")
                .HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(q => q.VatTotal,
            p => p.Property(x => x.Amount).HasColumnName("vat_total")
                .HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(q => q.GrandTotal,
            p => p.Property(x => x.Amount).HasColumnName("grand_total")
                .HasColumnType("decimal(19,2)").IsRequired());

        b.HasMany(q => q.Lines)
            .WithOne()
            .HasForeignKey(l => l.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(Quotation.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(q => q.QuotationNumber).HasDatabaseName("ix_quotations_quotation_number");
        b.HasIndex(q => q.CustomerId).HasDatabaseName("ix_quotations_customer_id");
        b.HasIndex(q => q.State).HasDatabaseName("ix_quotations_state");
    }
}

internal sealed class QuotationLineConfiguration : IEntityTypeConfiguration<QuotationLine>
{
    public void Configure(EntityTypeBuilder<QuotationLine> b)
    {
        b.ToTable("quotation_lines", schema: "documents");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(l => l.QuotationId).HasColumnName("quotation_id").IsRequired();
        b.Property(l => l.ItemId).HasColumnName("item_id").IsRequired();
        b.Property(l => l.Quantity)
            .HasColumnName("quantity").HasColumnType("decimal(19,4)").IsRequired();

        b.ComplexProperty(l => l.UnitPrice,
            p => p.Property(x => x.Amount).HasColumnName("unit_price")
                .HasColumnType("decimal(19,2)").IsRequired());

        b.Property(l => l.VatCategoryId).HasColumnName("vat_category_id").IsRequired();
        b.Property(l => l.VatRatePercent)
            .HasColumnName("vat_rate_percent").HasColumnType("decimal(5,2)").IsRequired();

        b.HasIndex(l => l.QuotationId).HasDatabaseName("ix_quotation_lines_quotation_id");
    }
}
