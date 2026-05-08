using EgyptTax.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.ToTable("attachments", schema: "documents");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(a => a.DocumentId).HasColumnName("document_id").IsRequired();
        b.Property(a => a.DocumentType)
            .HasColumnName("document_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        b.Property(a => a.FilenameOriginal)
            .HasColumnName("filename_original")
            .HasMaxLength(260)
            .IsRequired();
        b.Property(a => a.FilenameStorage)
            .HasColumnName("filename_storage")
            .HasMaxLength(260)
            .IsUnicode(false)
            .IsRequired();
        b.Property(a => a.RelativePath)
            .HasColumnName("relative_path")
            .HasMaxLength(512)
            .IsUnicode(false)
            .IsRequired();
        b.Property(a => a.Sha256).HasColumnName("sha256").HasColumnType("binary(32)").IsRequired();
        b.Property(a => a.MimeType)
            .HasColumnName("mime_type")
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();
        b.Property(a => a.SizeBytes).HasColumnName("size_bytes").IsRequired();
        b.Property(a => a.UploadedByUserId).HasColumnName("uploaded_by_user_id").IsRequired();
        b.Property(a => a.UploadedAtUtc)
            .HasColumnName("uploaded_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();

        // T141 — the MissingAttachmentRule probe loads attachments by
        // (document_id, document_type) keypair. Index keeps that
        // lookup seek-friendly as the table grows.
        b.HasIndex(a => new { a.DocumentId, a.DocumentType })
            .HasDatabaseName("ix_attachments_document_id_type");
    }
}
