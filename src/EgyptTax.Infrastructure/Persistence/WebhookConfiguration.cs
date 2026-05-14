using EgyptTax.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class WebhookConfiguration : IEntityTypeConfiguration<Webhook>
{
    public void Configure(EntityTypeBuilder<Webhook> b)
    {
        b.ToTable("webhooks", schema: "settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        b.Property(x => x.Url).HasColumnName("url").HasMaxLength(2000)
            .IsUnicode(false).IsRequired();
        b.Property(x => x.Secret).HasColumnName("secret").HasMaxLength(256)
            .IsUnicode(false).IsRequired();
        b.Property(x => x.EventMask).HasColumnName("event_mask")
            .HasMaxLength(500).IsUnicode(false).IsRequired();
        b.Property(x => x.Enabled).HasColumnName("enabled").IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        b.Property(x => x.LastDispatchAtUtc).HasColumnName("last_dispatch_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(x => x.LastDispatchStatus).HasColumnName("last_dispatch_status")
            .HasMaxLength(200);
    }
}
