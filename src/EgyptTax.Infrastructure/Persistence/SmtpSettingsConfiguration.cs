using EgyptTax.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class SmtpSettingsConfiguration : IEntityTypeConfiguration<SmtpSettings>
{
    public void Configure(EntityTypeBuilder<SmtpSettings> b)
    {
        b.ToTable("smtp_settings", schema: "settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(x => x.SendMethod)
            .HasColumnName("send_method")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        b.Property(x => x.Server).HasColumnName("server").HasMaxLength(200);
        b.Property(x => x.Port).HasColumnName("port").IsRequired();
        b.Property(x => x.Username).HasColumnName("username").HasMaxLength(200);
        b.Property(x => x.EncryptedPassword).HasColumnName("encrypted_password").HasMaxLength(2000);
        b.Property(x => x.FromAddress).HasColumnName("from_address").HasMaxLength(200);
        b.Property(x => x.FromName).HasColumnName("from_name").HasMaxLength(200);
        b.Property(x => x.UseTls).HasColumnName("use_tls").IsRequired();
    }
}
