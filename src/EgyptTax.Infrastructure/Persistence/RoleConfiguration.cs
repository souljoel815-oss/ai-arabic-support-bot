using EgyptTax.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles", schema: "identity");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(r => r.Code)
            .HasColumnName("code")
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();
        b.Property(r => r.RequiresMfa).HasColumnName("requires_mfa");
        b.HasIndex(r => r.Code).IsUnique().HasDatabaseName("ux_roles_code");

        b.ComplexProperty(
            r => r.Name,
            n =>
            {
                n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(100).IsRequired();
                n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(100).IsRequired();
            }
        );

        b.HasMany(r => r.Permissions)
            .WithMany()
            .UsingEntity(j => j.ToTable("role_permissions", schema: "identity"));
    }
}
