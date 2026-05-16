using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class CostCenterAllocationConfiguration
    : IEntityTypeConfiguration<CostCenterAllocation>
{
    public void Configure(EntityTypeBuilder<CostCenterAllocation> b)
    {
        b.ToTable("cost_center_allocations", schema: "master");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(a => a.SourceType)
            .HasColumnName("source_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        b.Property(a => a.SourceLineId).HasColumnName("source_line_id").IsRequired();
        b.Property(a => a.CostCenterId).HasColumnName("cost_center_id").IsRequired();
        b.Property(a => a.PercentBp).HasColumnName("percent_bp").IsRequired();
        b.Property(a => a.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();

        b.HasIndex(a => new { a.SourceType, a.SourceLineId })
            .HasDatabaseName("ix_cost_center_allocations_source");
        b.HasIndex(a => a.CostCenterId)
            .HasDatabaseName("ix_cost_center_allocations_cost_center_id");
    }
}
