using EgyptTax.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class SalesRepSettingsConfiguration : IEntityTypeConfiguration<SalesRepSettings>
{
    public void Configure(EntityTypeBuilder<SalesRepSettings> b)
    {
        b.ToTable("sales_rep_settings", schema: "settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.RequireApproval).HasColumnName("require_approval").IsRequired();
    }
}
