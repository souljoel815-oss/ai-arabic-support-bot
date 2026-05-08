using EgyptTax.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("permissions", schema: "identity");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(p => p.Code)
            .HasColumnName("code")
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();
        b.HasIndex(p => p.Code).IsUnique().HasDatabaseName("ux_permissions_code");

        b.ComplexProperty(
            p => p.Description,
            d =>
            {
                d.Property(x => x.Arabic)
                    .HasColumnName("description_ar")
                    .HasMaxLength(200)
                    .IsRequired();
                d.Property(x => x.English)
                    .HasColumnName("description_en")
                    .HasMaxLength(200)
                    .IsRequired();
            }
        );
    }
}
