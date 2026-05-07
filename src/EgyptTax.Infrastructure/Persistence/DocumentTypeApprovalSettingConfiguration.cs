using EgyptTax.Domain.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class DocumentTypeApprovalSettingConfiguration : IEntityTypeConfiguration<DocumentTypeApprovalSetting>
{
    public void Configure(EntityTypeBuilder<DocumentTypeApprovalSetting> b)
    {
        b.ToTable("document_type_approval_settings", schema: "workflow");
        b.HasKey(s => s.DocumentType);
        b.Property(s => s.DocumentType)
            .HasColumnName("document_type")
            .HasConversion<string>()
            .HasMaxLength(32);
        b.Property(s => s.ApprovalRequired).HasColumnName("approval_required");

        // FR-026 Phase-1 default: SalesInvoice posts directly so US1 ships
        // demoable; every other type follows the full approval lifecycle
        // by default.
        b.HasData(
            new DocumentTypeApprovalSetting(DocumentType.SalesInvoice, approvalRequired: false),
            new DocumentTypeApprovalSetting(DocumentType.CreditNote, approvalRequired: true),
            new DocumentTypeApprovalSetting(DocumentType.PurchaseInvoice, approvalRequired: true),
            new DocumentTypeApprovalSetting(DocumentType.Expense, approvalRequired: true),
            new DocumentTypeApprovalSetting(DocumentType.JournalVoucher, approvalRequired: true),
            new DocumentTypeApprovalSetting(DocumentType.SupplierPaymentVoucher, approvalRequired: true),
            new DocumentTypeApprovalSetting(DocumentType.CustomerReceiptVoucher, approvalRequired: true),
            new DocumentTypeApprovalSetting(DocumentType.FixedAsset, approvalRequired: true));
    }
}
