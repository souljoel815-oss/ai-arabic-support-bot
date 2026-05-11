using EgyptTax.Domain.Eta;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class EtaReceivedDocumentConfiguration : IEntityTypeConfiguration<EtaReceivedDocument>
{
    public void Configure(EntityTypeBuilder<EtaReceivedDocument> b)
    {
        b.ToTable("eta_received_documents", schema: "eta");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(s => s.RegulatorLongUuid)
            .HasColumnName("regulator_long_uuid")
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();
        b.HasIndex(s => s.RegulatorLongUuid)
            .IsUnique()
            .HasDatabaseName("ux_eta_received_documents_long_uuid");

        b.Property(s => s.SupplierTin)
            .HasColumnName("supplier_tin")
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        b.Property(s => s.SupplierLegalName)
            .HasColumnName("supplier_legal_name")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(s => s.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(64)
            .IsRequired();
        b.Property(s => s.DocumentDate)
            .HasColumnName("document_date")
            .HasColumnType("date")
            .IsRequired();

        b.Property(s => s.NetBeforeVatEgp)
            .HasColumnName("net_before_vat_egp")
            .HasColumnType("decimal(19,2)")
            .IsRequired();
        b.Property(s => s.VatTotalEgp)
            .HasColumnName("vat_total_egp")
            .HasColumnType("decimal(19,2)")
            .IsRequired();
        b.Property(s => s.GrandTotalEgp)
            .HasColumnName("grand_total_egp")
            .HasColumnType("decimal(19,2)")
            .IsRequired();

        b.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        b.Property(s => s.FirstSeenAtUtc)
            .HasColumnName("first_seen_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();
        b.Property(s => s.ResolvedAtUtc)
            .HasColumnName("resolved_at_utc")
            .HasColumnType("datetime2(3)");

        b.Property(s => s.ImportedAsPurchaseInvoiceId)
            .HasColumnName("imported_as_purchase_invoice_id");

        // Inbox query: list NeedsReview rows newest-first
        b.HasIndex(s => new { s.Status, s.FirstSeenAtUtc })
            .HasDatabaseName("ix_eta_received_documents_inbox");
    }
}
