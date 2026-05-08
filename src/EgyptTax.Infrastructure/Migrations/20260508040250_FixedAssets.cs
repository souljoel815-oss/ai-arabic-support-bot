using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixedAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fixed_assets",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    asset_category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    in_service_date = table.Column<DateOnly>(type: "date", nullable: false),
                    useful_life_months = table.Column<int>(type: "int", nullable: false),
                    depreciation_method = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    convention = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    disposed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    cost = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    description_ar = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    description_en = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    salvage_value = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixed_assets", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fixed_assets_in_service_date",
                schema: "documents",
                table: "fixed_assets",
                column: "in_service_date");

            migrationBuilder.CreateIndex(
                name: "ix_fixed_assets_status",
                schema: "documents",
                table: "fixed_assets",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_fixed_assets_code",
                schema: "documents",
                table: "fixed_assets",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fixed_assets",
                schema: "documents");
        }
    }
}
