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
        b.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(t => t.CompletedAtUtc).HasColumnName("completed_at_utc")
            .HasColumnType("datetime2(3)");
        b.HasIndex(t => t.ProjectId).HasDatabaseName("ix_project_tasks_project_id");
    }
}
