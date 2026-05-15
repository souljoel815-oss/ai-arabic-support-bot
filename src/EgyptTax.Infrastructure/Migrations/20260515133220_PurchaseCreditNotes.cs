using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseCreditNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "credit_note_of_purchase_invoice_id",
                schema: "documents",
                table: "purchase_invoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "credit_note_reason",
                schema: "documents",
                table: "purchase_invoices",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "credit_note_of_purchase_invoice_id",
                schema: "documents",
                table: "purchase_invoices");

            migrationBuilder.DropColumn(
                name: "credit_note_reason",
                schema: "documents",
                table: "purchase_invoices");
        }
    }
}
