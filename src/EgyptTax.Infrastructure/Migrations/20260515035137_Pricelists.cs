using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Pricelists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pricing");

            migrationBuilder.AddColumn<decimal>(
                name: "default_unit_price_egp",
                schema: "master",
                table: "items",
                type: "decimal(19,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "default_pricelist_id",
                schema: "master",
                table: "customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pricelists",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    name_ar = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    name_en = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricelists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pricelist_rules",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    pricelist_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    discount_percent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    fixed_price_egp = table.Column<decimal>(type: "decimal(19,2)", nullable: true),
                    sequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricelist_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_pricelist_rules_pricelists_pricelist_id",
                        column: x => x.pricelist_id,
                        principalSchema: "pricing",
                        principalTable: "pricelists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customers_default_pricelist_id",
                schema: "master",
                table: "customers",
                column: "default_pricelist_id");

            migrationBuilder.CreateIndex(
                name: "ix_pricelist_rules_item_id",
                schema: "pricing",
                table: "pricelist_rules",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_pricelist_rules_pricelist_id",
                schema: "pricing",
                table: "pricelist_rules",
                column: "pricelist_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pricelist_rules",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "pricelists",
                schema: "pricing");

            migrationBuilder.DropIndex(
                name: "ix_customers_default_pricelist_id",
                schema: "master",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "default_unit_price_egp",
                schema: "master",
                table: "items");

            migrationBuilder.DropColumn(
                name: "default_pricelist_id",
                schema: "master",
                table: "customers");
        }
    }
}
