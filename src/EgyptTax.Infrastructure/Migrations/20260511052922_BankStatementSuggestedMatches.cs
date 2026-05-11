using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BankStatementSuggestedMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "match_confidence_score",
                schema: "documents",
                table: "bank_statement_lines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "suggested_at_utc",
                schema: "documents",
                table: "bank_statement_lines",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "suggested_customer_receipt_voucher_id",
                schema: "documents",
                table: "bank_statement_lines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "suggested_supplier_payment_voucher_id",
                schema: "documents",
                table: "bank_statement_lines",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "match_confidence_score",
                schema: "documents",
                table: "bank_statement_lines");

            migrationBuilder.DropColumn(
                name: "suggested_at_utc",
                schema: "documents",
                table: "bank_statement_lines");

            migrationBuilder.DropColumn(
                name: "suggested_customer_receipt_voucher_id",
                schema: "documents",
                table: "bank_statement_lines");

            migrationBuilder.DropColumn(
                name: "suggested_supplier_payment_voucher_id",
                schema: "documents",
                table: "bank_statement_lines");
        }
    }
}
