using EgyptTax.Domain.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> b)
    {
        b.ToTable("approval_requests", schema: "workflow");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(r => r.DocumentId).HasColumnName("document_id").IsRequired();
        b.Property(r => r.DocumentType)
            .HasColumnName("document_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        b.Property(r => r.SubmittedByUserId).HasColumnName("submitted_by_user_id").IsRequired();
        b.Property(r => r.SubmittedAtUtc)
            .HasColumnName("submitted_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();

        b.Property(r => r.ApprovedByUserId).HasColumnName("approved_by_user_id");
        b.Property(r => r.ApprovedAtUtc)
            .HasColumnName("approved_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(r => r.RejectedByUserId).HasColumnName("rejected_by_user_id");
        b.Property(r => r.RejectedAtUtc)
            .HasColumnName("rejected_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(r => r.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);

        // The MyApprovalQueue + per-document approval-status lookups
        // both filter on DocumentId; the dashboard filter on Pending
        // status uses the IS-NULL composite predicate.
        b.HasIndex(r => r.DocumentId).HasDatabaseName("ix_approval_requests_document_id");
        b.HasIndex(r => r.SubmittedByUserId)
            .HasDatabaseName("ix_approval_requests_submitted_by_user_id");

        // Status is derived; the queue query orders by SubmittedAtUtc
        // among Pending rows (Approved+Rejected nullable both null).
        b.HasIndex(r => r.SubmittedAtUtc)
            .HasDatabaseName("ix_approval_requests_submitted_at_utc");
    }
}
