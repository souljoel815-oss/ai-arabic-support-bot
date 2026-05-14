using EgyptTax.Domain.Signatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class SignatureRequestConfiguration : IEntityTypeConfiguration<SignatureRequest>
{
    public void Configure(EntityTypeBuilder<SignatureRequest> b)
    {
        b.ToTable("signature_requests", schema: "signatures");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(s => s.DocumentType).HasColumnName("document_type")
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(s => s.DocumentId).HasColumnName("document_id").IsRequired();
        b.Property(s => s.Token).HasColumnName("token")
            .HasMaxLength(64).IsUnicode(false).IsRequired();
        b.HasIndex(s => s.Token).IsUnique().HasDatabaseName("ux_signature_requests_token");

        b.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(s => s.CreatedByUserId).HasColumnName("created_by_user_id");
        b.Property(s => s.ExpiresAtUtc).HasColumnName("expires_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(s => s.SignedAtUtc).HasColumnName("signed_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(s => s.SignerName).HasColumnName("signer_name").HasMaxLength(200);
        b.Property(s => s.SignerIp).HasColumnName("signer_ip")
            .HasMaxLength(45).IsUnicode(false);
        b.Property(s => s.Revoked).HasColumnName("revoked").IsRequired();

        b.HasIndex(s => new { s.DocumentType, s.DocumentId })
            .HasDatabaseName("ix_signature_requests_document");
    }
}
