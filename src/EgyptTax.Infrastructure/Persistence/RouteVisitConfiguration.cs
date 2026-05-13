using EgyptTax.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class RouteVisitConfiguration : IEntityTypeConfiguration<RouteVisit>
{
    public void Configure(EntityTypeBuilder<RouteVisit> b)
    {
        b.ToTable("route_visits", schema: "documents");
        b.HasKey(v => v.Id);
        b.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(v => v.RepUserId).HasColumnName("rep_user_id").IsRequired();
        b.Property(v => v.VisitDate).HasColumnName("visit_date").HasColumnType("date").IsRequired();
        b.Property(v => v.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(v => v.Sequence).HasColumnName("sequence").IsRequired();
        b.Property(v => v.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(v => v.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(v => v.PlannedNote).HasColumnName("planned_note").HasMaxLength(500);
        b.Property(v => v.VisitNote).HasColumnName("visit_note").HasMaxLength(500);
        b.Property(v => v.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime2(3)").IsRequired();
        b.Property(v => v.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("datetime2(3)");

        b.HasIndex(v => new { v.RepUserId, v.VisitDate, v.Sequence })
            .HasDatabaseName("ix_route_visits_rep_date_seq");
        b.HasIndex(v => v.CustomerId).HasDatabaseName("ix_route_visits_customer_id");
    }
}
