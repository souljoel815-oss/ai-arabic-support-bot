using EgyptTax.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class SalesTeamConfiguration : IEntityTypeConfiguration<SalesTeam>
{
    public void Configure(EntityTypeBuilder<SalesTeam> b)
    {
        b.ToTable("sales_teams", schema: "identity");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        b.ComplexProperty(t => t.Name, n =>
        {
            n.Property(x => x.Arabic)
                .HasColumnName("name_ar")
                .HasMaxLength(120)
                .IsRequired();
            n.Property(x => x.English)
                .HasColumnName("name_en")
                .HasMaxLength(120)
                .IsRequired();
        });

        b.Property(t => t.ManagerUserId).HasColumnName("manager_user_id");
        b.Property(t => t.MonthlyTargetEgp)
            .HasColumnName("monthly_target_egp")
            .HasColumnType("decimal(19,2)");

        b.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        b.Property(t => t.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();

        b.HasIndex(t => t.Status).HasDatabaseName("ix_sales_teams_status");
    }
}
