using EgyptTax.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> b)
    {
        b.ToTable("journal_entry_lines", schema: "accounting");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(l => l.JournalEntryId).HasColumnName("journal_entry_id").IsRequired();
        b.Property(l => l.AccountCode)
            .HasColumnName("account_code")
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();
        b.Property(l => l.Description).HasColumnName("description").HasMaxLength(256).IsRequired();

        b.ComplexProperty(
            l => l.Debit,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("debit")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.ComplexProperty(
            l => l.Credit,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("credit")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );

        b.HasIndex(l => l.JournalEntryId)
            .HasDatabaseName("ix_journal_entry_lines_journal_entry_id");
        b.HasIndex(l => l.AccountCode).HasDatabaseName("ix_journal_entry_lines_account_code");
    }
}
