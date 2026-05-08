using EgyptTax.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class AuditCheckpointRowConfiguration : IEntityTypeConfiguration<AuditCheckpointRow>
{
    public void Configure(EntityTypeBuilder<AuditCheckpointRow> b)
    {
        b.ToTable(
            "checkpoint",
            schema: "audit_meta",
            t => t.HasCheckConstraint("ck_audit_checkpoint_single_row", "[id] = 1")
        );

        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(c => c.LastIndex).HasColumnName("last_index");
        b.Property(c => c.LastHash).HasColumnName("last_hash").HasColumnType("binary(32)");
        b.Property(c => c.TsUtc).HasColumnName("ts_utc").HasColumnType("datetime2(3)");
    }
}
