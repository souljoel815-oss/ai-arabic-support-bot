using EgyptTax.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class LandedCostConfiguration : IEntityTypeConfiguration<LandedCost>
{
    public void Configure(EntityTypeBuilder<LandedCost> b)
    {
        b.ToTable("landed_costs", schema: "inventory");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(l => l.DocumentDate)
            .HasColumnName("document_date")
            .HasColumnType("date")
            .IsRequired();
        b.Property(l => l.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(32)
            .IsUnicode(false);
        b.Property(l => l.Note).HasColumnName("note").HasMaxLength(500);
        b.Property(l => l.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(l => l.SplitMethod)
            .HasColumnName("split_method")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(l => l.ValidatedAtUtc)
            .HasColumnName("validated_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(l => l.ValidatedByUserId).HasColumnName("validated_by_user_id");

        b.HasMany(l => l.Lines)
            .WithOne()
            .HasForeignKey(x => x.LandedCostId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Metadata.FindNavigation(nameof(LandedCost.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasMany(l => l.Allocations)
            .WithOne()
            .HasForeignKey(x => x.LandedCostId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Metadata.FindNavigation(nameof(LandedCost.Allocations))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(l => l.DocumentDate).HasDatabaseName("ix_landed_costs_document_date");
        b.HasIndex(l => l.DocumentNumber)
            .HasDatabaseName("ix_landed_costs_document_number")
            .HasFilter("[document_number] IS NOT NULL");
    }
}

internal sealed class LandedCostLineConfiguration : IEntityTypeConfiguration<LandedCostLine>
{
    public void Configure(EntityTypeBuilder<LandedCostLine> b)
    {
        b.ToTable("landed_cost_lines", schema: "inventory");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(l => l.LandedCostId).HasColumnName("landed_cost_id").IsRequired();
        b.Property(l => l.ClearingAccountCode)
            .HasColumnName("clearing_account_code")
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();
        b.ComplexProperty(
            l => l.Amount,
            p => p.Property(x => x.Amount)
                .HasColumnName("amount")
                .HasColumnType("decimal(19,2)")
                .IsRequired()
        );
        b.Property(l => l.Description).HasColumnName("description").HasMaxLength(200);
        b.HasIndex(l => l.LandedCostId).HasDatabaseName("ix_landed_cost_lines_landed_cost_id");
    }
}

internal sealed class LandedCostAllocationConfiguration : IEntityTypeConfiguration<LandedCostAllocation>
{
    public void Configure(EntityTypeBuilder<LandedCostAllocation> b)
    {
        b.ToTable("landed_cost_allocations", schema: "inventory");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(a => a.LandedCostId).HasColumnName("landed_cost_id").IsRequired();
        b.Property(a => a.PurchaseInvoiceLineId).HasColumnName("purchase_invoice_line_id").IsRequired();
        b.ComplexProperty(
            a => a.AllocatedAmount,
            p => p.Property(x => x.Amount)
                .HasColumnName("allocated_amount")
                .HasColumnType("decimal(19,2)")
                .IsRequired()
        );
        b.HasIndex(a => a.LandedCostId).HasDatabaseName("ix_landed_cost_allocations_landed_cost_id");
        b.HasIndex(a => a.PurchaseInvoiceLineId).HasDatabaseName("ix_landed_cost_allocations_pi_line_id");
    }
}
