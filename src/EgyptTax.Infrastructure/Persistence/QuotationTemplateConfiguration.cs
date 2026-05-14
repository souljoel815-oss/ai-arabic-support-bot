using EgyptTax.Domain.Quotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class QuotationTemplateConfiguration : IEntityTypeConfiguration<QuotationTemplate>
{
    public void Configure(EntityTypeBuilder<QuotationTemplate> b)
    {
        b.ToTable("quotation_templates", schema: "documents");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(t => t.Description).HasColumnName("description").HasMaxLength(2000);
        b.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(t => t.CreatedByUserId).HasColumnName("created_by_user_id");

        b.HasMany(t => t.Lines)
            .WithOne()
            .HasForeignKey(l => l.QuotationTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(t => t.IsActive).HasDatabaseName("ix_quotation_templates_is_active");
    }
}

internal sealed class QuotationTemplateLineConfiguration : IEntityTypeConfiguration<QuotationTemplateLine>
{
    public void Configure(EntityTypeBuilder<QuotationTemplateLine> b)
    {
        b.ToTable("quotation_template_lines", schema: "documents");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(l => l.QuotationTemplateId).HasColumnName("quotation_template_id").IsRequired();
        b.Property(l => l.ItemId).HasColumnName("item_id").IsRequired();
        b.Property(l => l.Quantity).HasColumnName("quantity")
            .HasColumnType("decimal(19,4)").IsRequired();
        b.ComplexProperty(l => l.UnitPrice,
            p => p.Property(x => x.Amount).HasColumnName("unit_price")
                .HasColumnType("decimal(19,4)").IsRequired());
        b.Property(l => l.VatCategoryId).HasColumnName("vat_category_id").IsRequired();
        b.Property(l => l.VatRatePercent).HasColumnName("vat_rate_percent")
            .HasColumnType("decimal(5,2)").IsRequired();
    }
}
