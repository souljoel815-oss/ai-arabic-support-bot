using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class DeductibleExpenseCategoryConfiguration
    : IEntityTypeConfiguration<DeductibleExpenseCategory>
{
    public void Configure(EntityTypeBuilder<DeductibleExpenseCategory> b)
    {
        b.ToTable("deductible_expense_categories", schema: "master");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(c => c.Code)
            .HasColumnName("code")
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();
        b.HasIndex(c => c.Code).IsUnique().HasDatabaseName("ux_deductible_expense_categories_code");

        b.ComplexProperty(
            c => c.Name,
            n =>
            {
                n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
                n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(200).IsRequired();
            }
        );

        b.Property(c => c.DefaultDeductible).HasColumnName("default_deductible").IsRequired();
        // FK to ChartOfAccount is documented but not enforced at the
        // DB layer until US4 lands the chart-of-accounts seed. The
        // Guid travels forward; the FK constraint is added when the
        // CoA migration creates the principal table.
        b.Property(c => c.DefaultAccountId).HasColumnName("default_account_id").IsRequired();
        b.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
    }
}
