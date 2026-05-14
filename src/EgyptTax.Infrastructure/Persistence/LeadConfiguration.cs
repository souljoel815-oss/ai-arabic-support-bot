using EgyptTax.Domain.Crm;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> b)
    {
        b.ToTable("leads", schema: "crm");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        b.ComplexProperty(l => l.Name, n =>
        {
            n.Property(p => p.Arabic).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
            n.Property(p => p.English).HasColumnName("name_en").HasMaxLength(200).IsRequired();
        });

        b.Property(l => l.CompanyName).HasColumnName("company_name").HasMaxLength(200);
        b.Property(l => l.Phone).HasColumnName("phone").HasMaxLength(32).IsUnicode(false);
        b.Property(l => l.Email).HasColumnName("email").HasMaxLength(254).IsUnicode(false);
        b.Property(l => l.Source).HasColumnName("source").HasMaxLength(100);

        b.Property(l => l.Stage)
            .HasColumnName("stage").HasConversion<string>().HasMaxLength(20).IsRequired();

        b.Property(l => l.AssignedToUserId).HasColumnName("assigned_to_user_id");
        b.Property(l => l.ExpectedCloseDate).HasColumnName("expected_close_date").HasColumnType("date");
        b.Property(l => l.ExpectedValueEgp).HasColumnName("expected_value_egp")
            .HasColumnType("decimal(19,2)");
        b.Property(l => l.NextAction).HasColumnName("next_action").HasMaxLength(500);
        b.Property(l => l.NextActionDate).HasColumnName("next_action_date").HasColumnType("date");

        b.Property(l => l.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(l => l.CreatedByUserId).HasColumnName("created_by_user_id");
        b.Property(l => l.WonAtUtc).HasColumnName("won_at_utc").HasColumnType("datetime2(3)");
        b.Property(l => l.LostAtUtc).HasColumnName("lost_at_utc").HasColumnType("datetime2(3)");
        b.Property(l => l.LostReason).HasColumnName("lost_reason").HasMaxLength(500);
        b.Property(l => l.ConvertedToCustomerId).HasColumnName("converted_to_customer_id");
        b.Property(l => l.ConvertedAtUtc).HasColumnName("converted_at_utc").HasColumnType("datetime2(3)");

        b.HasMany(l => l.Activities)
            .WithOne()
            .HasForeignKey(a => a.LeadId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Metadata.FindNavigation(nameof(Lead.Activities))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(l => l.Stage).HasDatabaseName("ix_leads_stage");
        b.HasIndex(l => l.AssignedToUserId).HasDatabaseName("ix_leads_assigned_to_user_id");
    }
}

internal sealed class LeadActivityConfiguration : IEntityTypeConfiguration<LeadActivity>
{
    public void Configure(EntityTypeBuilder<LeadActivity> b)
    {
        b.ToTable("lead_activities", schema: "crm");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(a => a.LeadId).HasColumnName("lead_id").IsRequired();
        b.Property(a => a.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(a => a.Note).HasColumnName("note").HasMaxLength(1000).IsRequired();
        b.Property(a => a.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("datetime2(3)").IsRequired();
        b.Property(a => a.LoggedByUserId).HasColumnName("logged_by_user_id");

        b.HasIndex(a => a.LeadId).HasDatabaseName("ix_lead_activities_lead_id");
    }
}
