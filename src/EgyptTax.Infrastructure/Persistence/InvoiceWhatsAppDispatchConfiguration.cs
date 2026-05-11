using EgyptTax.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class InvoiceWhatsAppDispatchConfiguration : IEntityTypeConfiguration<InvoiceWhatsAppDispatch>
{
    public void Configure(EntityTypeBuilder<InvoiceWhatsAppDispatch> b)
    {
        b.ToTable("invoice_whatsapp_dispatches", schema: "documents");
        b.HasKey(d => d.Id);
        b.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(d => d.SalesInvoiceId).HasColumnName("sales_invoice_id").IsRequired();
        b.Property(d => d.RecipientPhone).HasColumnName("recipient_phone").HasMaxLength(40).IsUnicode(false).IsRequired();
        b.Property(d => d.MessageBody).HasColumnName("message_body").HasMaxLength(2000).IsRequired();
        b.Property(d => d.SentAtUtc).HasColumnName("sent_at_utc").HasColumnType("datetime2(3)").IsRequired();
        b.Property(d => d.SentByUserId).HasColumnName("sent_by_user_id").IsRequired();

        b.Property(d => d.DeliveryStatus)
            .HasColumnName("delivery_status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();

        b.Property(d => d.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(100).IsUnicode(false);
        b.Property(d => d.DeliveredAtUtc).HasColumnName("delivered_at_utc").HasColumnType("datetime2(3)");
        b.Property(d => d.ReadAtUtc).HasColumnName("read_at_utc").HasColumnType("datetime2(3)");
        b.Property(d => d.FailedAtUtc).HasColumnName("failed_at_utc").HasColumnType("datetime2(3)");
        b.Property(d => d.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);

        // Drives the per-invoice dispatch history block on the
        // sales-invoice detail page.
        b.HasIndex(d => new { d.SalesInvoiceId, d.SentAtUtc })
            .HasDatabaseName("ix_invoice_whatsapp_dispatches_invoice_sent");

        // Webhook-correlation index: when Meta posts a delivery /
        // read confirmation it carries the provider id.
        b.HasIndex(d => d.ProviderMessageId)
            .HasDatabaseName("ix_invoice_whatsapp_dispatches_provider_id");
    }
}
