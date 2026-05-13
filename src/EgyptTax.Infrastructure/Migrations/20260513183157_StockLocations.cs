using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StockLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "master_data");

            migrationBuilder.CreateTable(
                name: "item_stock_by_location",
                schema: "master_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    location_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(19,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_stock_by_location", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_locations",
                schema: "master_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    is_default = table.Column<bool>(type: "bit", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    name_ar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_locations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_item_stock_by_location_item_location",
                schema: "master_data",
                table: "item_stock_by_location",
                columns: new[] { "item_id", "location_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_stock_locations_code",
                schema: "master_data",
                table: "stock_locations",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_stock_by_location",
                schema: "master_data");

            migrationBuilder.DropTable(
                name: "stock_locations",
                schema: "master_data");
        }
    }
}
