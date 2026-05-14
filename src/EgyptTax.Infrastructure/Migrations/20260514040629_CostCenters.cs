using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CostCenters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cost_center_id",
                schema: "documents",
                table: "expenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cost_centers",
                schema: "master_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    name_ar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_centers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expenses_cost_center_id",
                schema: "documents",
                table: "expenses",
                column: "cost_center_id");

            migrationBuilder.CreateIndex(
                name: "ux_cost_centers_code",
                schema: "master_data",
                table: "cost_centers",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cost_centers",
                schema: "master_data");

            migrationBuilder.DropIndex(
                name: "ix_expenses_cost_center_id",
                schema: "documents",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "cost_center_id",
                schema: "documents",
                table: "expenses");
        }
    }
}
