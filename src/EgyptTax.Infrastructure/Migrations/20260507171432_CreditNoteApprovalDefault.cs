using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// FR-013 follow-on: Phase-1 demo-ability extends to credit notes.
    /// The original `DocumentTypeApprovalSettings` migration seeded
    /// CreditNote with approval_required = true (the conservative
    /// default for a tax-impacting operation), but US1 needs to ship
    /// the FR-013 correction path end-to-end without an approval
    /// workflow first. Operators can flip CreditNote back to
    /// approval-required via the (future) admin UI; this migration
    /// just changes the seeded default so a fresh install is
    /// demo-ready.
    /// </remarks>
    public partial class CreditNoteApprovalDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [workflow].[document_type_approval_settings] SET [approval_required] = 0 WHERE [document_type] = N'CreditNote';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [workflow].[document_type_approval_settings] SET [approval_required] = 1 WHERE [document_type] = N'CreditNote';");
        }
    }
}
