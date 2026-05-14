using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReorderRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reorder_rules",
                schema: "master_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    min_quantity = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    target_quantity = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    preferred_supplier_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reorder_rules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_reorder_rules_item_id",
                schema: "master_data",
                table: "reorder_rules",
                column: "item_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reorder_rules",
                schema: "master_data");
        }
    }
}
