using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// US2 follow-on (mirrors CreditNoteApprovalDefault from US1):
    /// the original DocumentTypeApprovalSettings migration seeded
    /// PurchaseInvoice with approval_required = true (the
    /// conservative default for tax-impacting documents). Phase 1
    /// ships the buy-side flow without an approval workflow first
    /// so the operator can post a purchase invoice end-to-end;
    /// operators can flip this back via the (future) admin UI.
    /// </remarks>
    public partial class PurchaseInvoiceApprovalDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [workflow].[document_type_approval_settings] SET [approval_required] = 0 WHERE [document_type] = N'PurchaseInvoice';"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [workflow].[document_type_approval_settings] SET [approval_required] = 1 WHERE [document_type] = N'PurchaseInvoice';"
            );
        }
    }
}
