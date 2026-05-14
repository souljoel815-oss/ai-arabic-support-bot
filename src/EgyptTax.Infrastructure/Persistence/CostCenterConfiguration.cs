using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> b)
    {
        b.ToTable("cost_centers", schema: "master_data");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(c => c.Code).HasColumnName("code").HasMaxLength(32).IsUnicode(false).IsRequired();
        b.HasIndex(c => c.Code).IsUnique().HasDatabaseName("ux_cost_centers_code");

        b.ComplexProperty(c => c.Name, n =>
        {
            n.Property(p => p.Arabic).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
            n.Property(p => p.English).HasColumnName("name_en").HasMaxLength(200).IsRequired();
        });

        b.Property(c => c.Status)
            .HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();
    }
}
