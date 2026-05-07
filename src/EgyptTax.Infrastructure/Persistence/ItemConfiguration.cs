using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> b)
    {
        b.ToTable("items", schema: "master");
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(i => i.Code).HasColumnName("code").HasMaxLength(32).IsUnicode(false).IsRequired();
        b.HasIndex(i => i.Code).IsUnique().HasDatabaseName("ux_items_code");

        b.ComplexProperty(i => i.Name, n =>
        {
            n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
            n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(200).IsRequired();
        });

        b.Property(i => i.DefaultVatCategoryId).HasColumnName("default_vat_category_id").IsRequired();
        b.Property(i => i.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();
    }
}
