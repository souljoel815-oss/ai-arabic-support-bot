using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StockAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_adjustments",
                schema: "master_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    location_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    counted_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    counted_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    system_count = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    actual_count = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    delta = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_adjustments_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "master",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_adjustments_stock_locations_location_id",
                        column: x => x.location_id,
                        principalSchema: "master_data",
                        principalTable: "stock_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustments_item_counted",
                schema: "master_data",
                table: "stock_adjustments",
                columns: new[] { "item_id", "counted_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustments_location_counted",
                schema: "master_data",
                table: "stock_adjustments",
                columns: new[] { "location_id", "counted_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_adjustments",
                schema: "master_data");
        }
    }
}
