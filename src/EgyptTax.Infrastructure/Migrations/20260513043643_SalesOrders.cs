using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalesOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_orders",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_number = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: false),
                    state = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    confirmed_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    converted_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    converted_to_invoice_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    grand_total = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    subtotal = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    vat_total = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_lines",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sales_order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(19,3)", nullable: false),
                    vat_category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    vat_rate_percent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_order_lines_sales_orders_sales_order_id",
                        column: x => x.sales_order_id,
                        principalSchema: "documents",
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_lines_sales_order_id",
                schema: "documents",
                table: "sales_order_lines",
                column: "sales_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_created_by_user_id",
                schema: "documents",
                table: "sales_orders",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_customer_id",
                schema: "documents",
                table: "sales_orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_state",
                schema: "documents",
                table: "sales_orders",
                column: "state");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_order_lines",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "sales_orders",
                schema: "documents");
        }
    }
}
