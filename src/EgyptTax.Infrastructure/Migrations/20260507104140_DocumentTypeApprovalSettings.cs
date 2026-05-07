using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DocumentTypeApprovalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workflow");

            migrationBuilder.CreateTable(
                name: "document_type_approval_settings",
                schema: "workflow",
                columns: table => new
                {
                    document_type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    approval_required = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_type_approval_settings", x => x.document_type);
                });

            migrationBuilder.InsertData(
                schema: "workflow",
                table: "document_type_approval_settings",
                columns: new[] { "document_type", "approval_required" },
                values: new object[,]
                {
                    { "CreditNote", true },
                    { "CustomerReceiptVoucher", true },
                    { "Expense", true },
                    { "FixedAsset", true },
                    { "JournalVoucher", true },
                    { "PurchaseInvoice", true },
                    { "SalesInvoice", false },
                    { "SupplierPaymentVoucher", true }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_type_approval_settings",
                schema: "workflow");
        }
    }
}
