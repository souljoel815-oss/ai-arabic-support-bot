using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class CashAccountConfiguration : IEntityTypeConfiguration<CashAccount>
{
    public void Configure(EntityTypeBuilder<CashAccount> b)
    {
        b.ToTable("cash_accounts", schema: "master");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(c => c.AccountCode)
            .HasColumnName("account_code")
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();
        b.HasIndex(c => c.AccountCode).IsUnique().HasDatabaseName("ux_cash_accounts_code");

        b.ComplexProperty(
            c => c.Name,
            n =>
            {
                n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
                n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(200).IsRequired();
            });

        b.Property(c => c.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(8)
            .IsUnicode(false)
            .IsRequired();
        b.Property(c => c.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsUnicode(false)
            .IsRequired();
        b.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();
        b.Property(c => c.IsDefault).HasColumnName("is_default").IsRequired();

        b.Property(c => c.BankName).HasColumnName("bank_name").HasMaxLength(200);
        b.Property(c => c.AccountNumber).HasColumnName("account_number").HasMaxLength(64).IsUnicode(false);
        b.Property(c => c.BranchName).HasColumnName("branch_name").HasMaxLength(200);
        b.Property(c => c.IbanOrSwift).HasColumnName("iban_or_swift").HasMaxLength(64).IsUnicode(false);

        // The default account is picked by the unique partial index;
        // at most one row may have is_default = 1.
        b.HasIndex(c => c.IsDefault)
            .HasDatabaseName("ux_cash_accounts_default")
            .IsUnique()
            .HasFilter("[is_default] = 1");
    }
}
