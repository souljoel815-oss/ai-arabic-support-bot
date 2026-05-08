using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreditNoteFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "credit_note_of_invoice_id",
                schema: "documents",
                table: "sales_invoices",
                type: "uniqueidentifier",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "credit_note_reason",
                schema: "documents",
                table: "sales_invoices",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_credit_note_of_invoice_id",
                schema: "documents",
                table: "sales_invoices",
                column: "credit_note_of_invoice_id",
                filter: "[credit_note_of_invoice_id] IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sales_invoices_credit_note_of_invoice_id",
                schema: "documents",
                table: "sales_invoices"
            );

            migrationBuilder.DropColumn(
                name: "credit_note_of_invoice_id",
                schema: "documents",
                table: "sales_invoices"
            );

            migrationBuilder.DropColumn(
                name: "credit_note_reason",
                schema: "documents",
                table: "sales_invoices"
            );
        }
    }
}
