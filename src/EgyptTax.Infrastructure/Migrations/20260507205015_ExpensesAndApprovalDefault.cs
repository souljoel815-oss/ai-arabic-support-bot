using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpensesAndApprovalDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expenses",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_date = table.Column<DateOnly>(type: "date", nullable: false),
                    category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    deductible_flag = table.Column<bool>(type: "bit", nullable: false),
                    state = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    document_number = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    posted_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    posted_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    posting_mode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    amount = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    description_ar = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    description_en = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expenses_category_id",
                schema: "documents",
                table: "expenses",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_document_date",
                schema: "documents",
                table: "expenses",
                column: "document_date");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_document_number",
                schema: "documents",
                table: "expenses",
                column: "document_number");

            // Flip the seeded DocumentTypeApprovalSetting for Expense
            // to false for Phase-1 demo-ability (mirrors the
            // CreditNoteApprovalDefault + PurchaseInvoiceApprovalDefault
            // migrations from US1/US2). Operators can re-enable approval
            // via the (future) admin UI.
            migrationBuilder.Sql(
                "UPDATE [workflow].[document_type_approval_settings] SET [approval_required] = 0 WHERE [document_type] = N'Expense';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [workflow].[document_type_approval_settings] SET [approval_required] = 1 WHERE [document_type] = N'Expense';");
            migrationBuilder.DropTable(
                name: "expenses",
                schema: "documents");
        }
    }
}
