using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptDiscountAndCostCenterAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "discount_taken_amount",
                schema: "documents",
                table: "customer_receipt_vouchers",
                type: "decimal(19,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "cost_center_allocations",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    source_type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    source_line_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    cost_center_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    percent_bp = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_center_allocations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cost_center_allocations_cost_center_id",
                schema: "master",
                table: "cost_center_allocations",
                column: "cost_center_id");

            migrationBuilder.CreateIndex(
                name: "ix_cost_center_allocations_source",
                schema: "master",
                table: "cost_center_allocations",
                columns: new[] { "source_type", "source_line_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cost_center_allocations",
                schema: "master");

            migrationBuilder.DropColumn(
                name: "discount_taken_amount",
                schema: "documents",
                table: "customer_receipt_vouchers");
        }
    }
}
