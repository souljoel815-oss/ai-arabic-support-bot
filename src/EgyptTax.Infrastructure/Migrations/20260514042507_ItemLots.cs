using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ItemLots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "tracks_lots",
                schema: "master",
                table: "items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "item_lots",
                schema: "master_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    lot_code = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    quantity_on_hand = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    received_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    supplier_reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_lots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_item_lots_expiry_date",
                schema: "master_data",
                table: "item_lots",
                column: "expiry_date");

            migrationBuilder.CreateIndex(
                name: "ux_item_lots_item_lot_code",
                schema: "master_data",
                table: "item_lots",
                columns: new[] { "item_id", "lot_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_lots",
                schema: "master_data");

            migrationBuilder.DropColumn(
                name: "tracks_lots",
                schema: "master",
                table: "items");
        }
    }
}
