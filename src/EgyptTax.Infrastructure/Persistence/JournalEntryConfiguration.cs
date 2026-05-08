using EgyptTax.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> b)
    {
        b.ToTable("journal_entries", schema: "accounting");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(e => e.SourceDocumentId).HasColumnName("source_document_id").IsRequired();
        b.Property(e => e.SourceDocumentNumber)
            .HasColumnName("source_document_number")
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();
        b.Property(e => e.SourceDocumentType)
            .HasColumnName("source_document_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        b.Property(e => e.PostedAtUtc)
            .HasColumnName("posted_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();

        b.HasMany(e => e.Lines)
            .WithOne()
            .HasForeignKey(l => l.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(JournalEntry.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(e => e.SourceDocumentId)
            .HasDatabaseName("ix_journal_entries_source_document_id");
        b.HasIndex(e => e.SourceDocumentNumber)
            .HasDatabaseName("ix_journal_entries_source_document_number");
    }
}
