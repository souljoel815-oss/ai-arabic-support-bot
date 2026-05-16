using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalesInvoiceLineSerialIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "serial_ids_json",
                schema: "documents",
                table: "sales_invoice_lines",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "serial_ids_json",
                schema: "documents",
                table: "sales_invoice_lines");
        }
    }
}
