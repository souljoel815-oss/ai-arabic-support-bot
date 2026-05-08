using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceLevelDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "invoice_level_discount_amount",
                schema: "documents",
                table: "sales_invoices",
                type: "decimal(19,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "invoice_level_discount_percent",
                schema: "documents",
                table: "sales_invoices",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "net_before_vat",
                schema: "documents",
                table: "sales_invoices",
                type: "decimal(19,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "line_apportioned_discount",
                schema: "documents",
                table: "sales_invoice_lines",
                type: "decimal(19,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "line_net_subtotal",
                schema: "documents",
                table: "sales_invoice_lines",
                type: "decimal(19,2)",
                nullable: false,
                defaultValue: 0m
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "invoice_level_discount_amount",
                schema: "documents",
                table: "sales_invoices"
            );

            migrationBuilder.DropColumn(
                name: "invoice_level_discount_percent",
                schema: "documents",
                table: "sales_invoices"
            );

            migrationBuilder.DropColumn(
                name: "net_before_vat",
                schema: "documents",
                table: "sales_invoices"
            );

            migrationBuilder.DropColumn(
                name: "line_apportioned_discount",
                schema: "documents",
                table: "sales_invoice_lines"
            );

            migrationBuilder.DropColumn(
                name: "line_net_subtotal",
                schema: "documents",
                table: "sales_invoice_lines"
            );
        }
    }
}
