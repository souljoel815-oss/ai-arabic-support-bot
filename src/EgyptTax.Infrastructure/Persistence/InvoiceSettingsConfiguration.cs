using EgyptTax.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class InvoiceSettingsConfiguration : IEntityTypeConfiguration<InvoiceSettings>
{
    public void Configure(EntityTypeBuilder<InvoiceSettings> b)
    {
        b.ToTable("invoice_settings", schema: "settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(x => x.TemplateId).HasColumnName("template_id").HasMaxLength(40).IsRequired();
        b.Property(x => x.Language)
            .HasColumnName("language")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        b.Property(x => x.Prefix).HasColumnName("prefix").HasMaxLength(20).IsRequired();
        b.Property(x => x.NextNumber).HasColumnName("next_number").IsRequired();
        b.Property(x => x.DefaultPaymentTermsDays).HasColumnName("default_payment_terms_days").IsRequired();
        b.Property(x => x.FooterNotes).HasColumnName("footer_notes").HasMaxLength(2000);
        b.Property(x => x.ShowQrCode).HasColumnName("show_qr_code").IsRequired();
        b.Property(x => x.ShowLogo).HasColumnName("show_logo").IsRequired();
    }
}
