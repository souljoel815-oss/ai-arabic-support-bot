using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BankStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank_statements",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    cash_account_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    source_file_name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    imported_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    closing_balance = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    opening_balance = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_statements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bank_statement_lines",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    statement_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    bank_reference = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    matched_supplier_payment_voucher_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    matched_customer_receipt_voucher_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    matched_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    matched_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    credit = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    debit = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    running_balance = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_statement_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_statement_lines_bank_statements_statement_id",
                        column: x => x.statement_id,
                        principalSchema: "documents",
                        principalTable: "bank_statements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bank_statement_lines_statement_id",
                schema: "documents",
                table: "bank_statement_lines",
                column: "statement_id");

            migrationBuilder.CreateIndex(
                name: "ix_bank_statement_lines_status_date",
                schema: "documents",
                table: "bank_statement_lines",
                columns: new[] { "status", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "ix_bank_statements_account_period",
                schema: "documents",
                table: "bank_statements",
                columns: new[] { "cash_account_id", "period_start" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_statement_lines",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "bank_statements",
                schema: "documents");
        }
    }
}
