using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FiscalPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fiscal_positions",
                schema: "master_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    country_auto_apply = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    name_ar = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    name_en = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_positions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fiscal_position_mappings",
                schema: "master_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    fiscal_position_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    source_vat_category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    destination_vat_category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_position_mappings", x => x.id);
                    table.ForeignKey(
                        name: "FK_fiscal_position_mappings_fiscal_positions_fiscal_position_id",
                        column: x => x.fiscal_position_id,
                        principalSchema: "master_data",
                        principalTable: "fiscal_positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_position_mappings_fiscal_position_id",
                schema: "master_data",
                table: "fiscal_position_mappings",
                column: "fiscal_position_id");

            migrationBuilder.CreateIndex(
                name: "ix_fiscal_positions_is_active",
                schema: "master_data",
                table: "fiscal_positions",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fiscal_position_mappings",
                schema: "master_data");

            migrationBuilder.DropTable(
                name: "fiscal_positions",
                schema: "master_data");
        }
    }
}
