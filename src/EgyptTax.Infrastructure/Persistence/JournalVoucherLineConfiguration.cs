using EgyptTax.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class JournalVoucherLineConfiguration : IEntityTypeConfiguration<JournalVoucherLine>
{
    public void Configure(EntityTypeBuilder<JournalVoucherLine> b)
    {
        b.ToTable("journal_voucher_lines", schema: "accounting");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(l => l.JournalVoucherId).HasColumnName("journal_voucher_id").IsRequired();
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

        b.HasIndex(l => l.JournalVoucherId)
            .HasDatabaseName("ix_journal_voucher_lines_journal_voucher_id");
        b.HasIndex(l => l.AccountCode).HasDatabaseName("ix_journal_voucher_lines_account_code");
    }
}
