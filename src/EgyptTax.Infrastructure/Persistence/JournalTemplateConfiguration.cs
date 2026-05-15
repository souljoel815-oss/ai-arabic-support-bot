using EgyptTax.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class JournalTemplateConfiguration : IEntityTypeConfiguration<JournalTemplate>
{
    public void Configure(EntityTypeBuilder<JournalTemplate> b)
    {
        b.ToTable("journal_templates", schema: "accounting");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(t => t.Description).HasColumnName("description").HasMaxLength(2000);
        b.Property(t => t.Schedule).HasColumnName("schedule")
            .HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(t => t.AutoReverse).HasColumnName("auto_reverse").IsRequired();
        b.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(t => t.CreatedByUserId).HasColumnName("created_by_user_id");

        b.HasMany(t => t.Lines)
            .WithOne()
            .HasForeignKey(l => l.JournalTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(JournalTemplate.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(t => t.IsActive).HasDatabaseName("ix_journal_templates_is_active");
    }
}

internal sealed class JournalTemplateLineConfiguration : IEntityTypeConfiguration<JournalTemplateLine>
{
    public void Configure(EntityTypeBuilder<JournalTemplateLine> b)
    {
        b.ToTable("journal_template_lines", schema: "accounting");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(l => l.JournalTemplateId).HasColumnName("journal_template_id").IsRequired();
        b.Property(l => l.AccountCode).HasColumnName("account_code")
            .HasMaxLength(16).IsUnicode(false).IsRequired();
        b.ComplexProperty(l => l.Debit,
            p => p.Property(x => x.Amount).HasColumnName("debit")
                .HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(l => l.Credit,
            p => p.Property(x => x.Amount).HasColumnName("credit")
                .HasColumnType("decimal(19,2)").IsRequired());
        b.Property(l => l.Description).HasColumnName("description")
            .HasMaxLength(500).IsRequired();
    }
}
