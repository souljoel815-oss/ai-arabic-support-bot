using EgyptTax.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class TimesheetEntryConfiguration : IEntityTypeConfiguration<TimesheetEntry>
{
    public void Configure(EntityTypeBuilder<TimesheetEntry> b)
    {
        b.ToTable("timesheet_entries", schema: "projects");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        b.Property(t => t.EntryDate).HasColumnName("entry_date").HasColumnType("date").IsRequired();
        b.Property(t => t.ProjectId).HasColumnName("project_id").IsRequired();
        b.Property(t => t.ProjectTaskId).HasColumnName("project_task_id");
        b.Property(t => t.Hours).HasColumnName("hours").HasColumnType("decimal(5,2)").IsRequired();
        b.Property(t => t.Billable).HasColumnName("billable").IsRequired();
        b.Property(t => t.HourlyRateEgp)
            .HasColumnName("hourly_rate_egp")
            .HasColumnType("decimal(10,2)")
            .IsRequired();
        b.Property(t => t.Note).HasColumnName("note").HasMaxLength(500);
        b.Property(t => t.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();

        b.HasIndex(t => new { t.UserId, t.EntryDate })
            .HasDatabaseName("ix_timesheet_entries_user_date");
        b.HasIndex(t => t.ProjectId).HasDatabaseName("ix_timesheet_entries_project_id");
    }
}
