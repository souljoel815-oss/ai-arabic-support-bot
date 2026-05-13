using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ItemInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "low_stock_threshold",
                schema: "master",
                table: "items",
                type: "decimal(19,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity_on_hand",
                schema: "master",
                table: "items",
                type: "decimal(19,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "stock_movements",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(19,3)", nullable: false),
                    quantity_on_hand_after = table.Column<decimal>(type: "decimal(19,3)", nullable: false),
                    kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    source_document_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_movements_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "master",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_item_occurred",
                schema: "master",
                table: "stock_movements",
                columns: new[] { "item_id", "occurred_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_movements",
                schema: "master");

            migrationBuilder.DropColumn(
                name: "low_stock_threshold",
                schema: "master",
                table: "items");

            migrationBuilder.DropColumn(
                name: "quantity_on_hand",
                schema: "master",
                table: "items");
        }
    }
}
