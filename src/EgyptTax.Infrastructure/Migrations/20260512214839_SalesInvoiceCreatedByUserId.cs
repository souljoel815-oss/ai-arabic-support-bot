using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalesInvoiceCreatedByUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                schema: "documents",
                table: "sales_invoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_created_by_user_id",
                schema: "documents",
                table: "sales_invoices",
                column: "created_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sales_invoices_created_by_user_id",
                schema: "documents",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                schema: "documents",
                table: "sales_invoices");
        }
    }
}
