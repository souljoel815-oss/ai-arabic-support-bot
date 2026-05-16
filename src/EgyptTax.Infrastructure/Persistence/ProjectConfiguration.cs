using EgyptTax.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("projects", schema: "projects");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(p => p.Code).HasColumnName("code").HasMaxLength(32).IsUnicode(false).IsRequired();
        b.HasIndex(p => p.Code).IsUnique().HasDatabaseName("ux_projects_code");

        b.ComplexProperty(p => p.Name, n =>
        {
            n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
            n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(200).IsRequired();
        });

        b.Property(p => p.CustomerId).HasColumnName("customer_id");
        b.Property(p => p.StartDate).HasColumnName("start_date").HasColumnType("date").IsRequired();
        b.Property(p => p.EndDate).HasColumnName("end_date").HasColumnType("date");
        b.Property(p => p.BudgetEgp).HasColumnName("budget_egp").HasColumnType("decimal(19,2)");
        b.Property(p => p.Status).HasColumnName("status")
            .HasConversion<string>().HasMaxLength(16).IsRequired();
        // v5 D.2.2 — optional cost-center link for P&L roll-up.
        b.Property(p => p.LinkedCostCenterId).HasColumnName("linked_cost_center_id");
        b.HasIndex(p => p.LinkedCostCenterId).HasDatabaseName("ix_projects_linked_cost_center_id");

        b.HasMany(p => p.Tasks).WithOne()
            .HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.Metadata.FindNavigation(nameof(Project.Tasks))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> b)
    {
        b.ToTable("project_tasks", schema: "projects");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(t => t.ProjectId).HasColumnName("project_id").IsRequired();
        b.Property(t => t.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        b.Property(t => t.Status).HasColumnName("status")
            .HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(t => t.AssignedToUserId).HasColumnName("assigned_to_user_id");
        b.Property(t => t.DueDate).HasColumnName("due_date").HasColumnType("date");
        // v5 D.2.3 — planned start date for the Gantt chart.
        b.Property(t => t.StartDate).HasColumnName("start_date").HasColumnType("date");
        b.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(t => t.CompletedAtUtc).HasColumnName("completed_at_utc")
            .HasColumnType("datetime2(3)");

        // v5 A.3 — task priority + sub-tasks. NB: no HasDefaultValue
        // here — TaskPriority.Urgent is enum value 0 which EF would
        // treat as "use the DB default" (silently demoting Urgent to
        // Normal at insert). The C# constructor defaults to Normal;
        // the migration backfills existing rows to 'Normal'.
        b.Property(t => t.Priority).HasColumnName("priority")
            .HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(t => t.ParentTaskId).HasColumnName("parent_task_id");
        b.HasIndex(t => t.ParentTaskId).HasDatabaseName("ix_project_tasks_parent");

        b.HasIndex(t => t.ProjectId).HasDatabaseName("ix_project_tasks_project_id");
    }
}
