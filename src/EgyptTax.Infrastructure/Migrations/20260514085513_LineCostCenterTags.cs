using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LineCostCenterTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cost_center_id",
                schema: "documents",
                table: "sales_invoice_lines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cost_center_id",
                schema: "documents",
                table: "purchase_invoice_lines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_lines_cost_center",
                schema: "documents",
                table: "sales_invoice_lines",
                column: "cost_center_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_lines_cost_center",
                schema: "documents",
                table: "purchase_invoice_lines",
                column: "cost_center_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sales_invoice_lines_cost_center",
                schema: "documents",
                table: "sales_invoice_lines");

            migrationBuilder.DropIndex(
                name: "ix_purchase_invoice_lines_cost_center",
                schema: "documents",
                table: "purchase_invoice_lines");

            migrationBuilder.DropColumn(
                name: "cost_center_id",
                schema: "documents",
                table: "sales_invoice_lines");

            migrationBuilder.DropColumn(
                name: "cost_center_id",
                schema: "documents",
                table: "purchase_invoice_lines");
        }
    }
}
