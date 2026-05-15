using EgyptTax.Domain.Pos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class PosSessionConfiguration : IEntityTypeConfiguration<PosSession>
{
    public void Configure(EntityTypeBuilder<PosSession> b)
    {
        b.ToTable("pos_sessions", schema: "pos");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(s => s.OpenedByUserId).HasColumnName("opened_by_user_id").IsRequired();
        b.Property(s => s.OpenedAtUtc).HasColumnName("opened_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.ComplexProperty(s => s.OpeningCash,
            p => p.Property(x => x.Amount).HasColumnName("opening_cash")
                .HasColumnType("decimal(19,2)").IsRequired());

        b.Property(s => s.ClosedByUserId).HasColumnName("closed_by_user_id");
        b.Property(s => s.ClosedAtUtc).HasColumnName("closed_at_utc").HasColumnType("datetime2(3)");

        b.Property(s => s.ExpectedClosingCashEgp).HasColumnName("expected_closing_cash")
            .HasColumnType("decimal(19,2)");
        b.Property(s => s.ActualClosingCashEgp).HasColumnName("actual_closing_cash")
            .HasColumnType("decimal(19,2)");
        b.Property(s => s.VarianceEgp).HasColumnName("variance")
            .HasColumnType("decimal(19,2)");

        b.Property(s => s.Notes).HasColumnName("notes").HasMaxLength(500);

        b.Ignore(s => s.Status);

        b.HasIndex(s => s.OpenedAtUtc).HasDatabaseName("ix_pos_sessions_opened_at");
    }
}
