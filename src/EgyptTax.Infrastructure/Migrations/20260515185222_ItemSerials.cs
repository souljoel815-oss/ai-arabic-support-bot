using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ItemSerials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "tracks_serials",
                schema: "master",
                table: "items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "item_serials",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    serial_number = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    lot_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    current_location_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    current_customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    sold_on_sales_invoice_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    last_status_change_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_serials", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_item_serials_status",
                schema: "master",
                table: "item_serials",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_item_serials_item_serial",
                schema: "master",
                table: "item_serials",
                columns: new[] { "item_id", "serial_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_serials",
                schema: "master");

            migrationBuilder.DropColumn(
                name: "tracks_serials",
                schema: "master",
                table: "items");
        }
    }
}
