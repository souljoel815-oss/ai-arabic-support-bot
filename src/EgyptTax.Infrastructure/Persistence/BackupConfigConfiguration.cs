using EgyptTax.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class BackupConfigConfiguration : IEntityTypeConfiguration<BackupConfig>
{
    public void Configure(EntityTypeBuilder<BackupConfig> b)
    {
        b.ToTable("backup_config", schema: "settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(x => x.AutoBackupEnabled).HasColumnName("auto_backup_enabled").IsRequired();
        b.Property(x => x.Frequency)
            .HasColumnName("frequency")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        b.Property(x => x.SavePath).HasColumnName("save_path").HasMaxLength(500).IsRequired();
        b.Property(x => x.RetentionCount).HasColumnName("retention_count").IsRequired();
        b.Property(x => x.LastBackupAtUtc).HasColumnName("last_backup_at_utc").HasColumnType("datetime2(3)");
        b.Property(x => x.LastBackupSizeBytes).HasColumnName("last_backup_size_bytes");
        b.Property(x => x.LastBackupPath).HasColumnName("last_backup_path").HasMaxLength(500);
    }
}
