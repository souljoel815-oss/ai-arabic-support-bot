using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ItemPhysicalAttrs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "volume_m3",
                schema: "master",
                table: "items",
                type: "decimal(10,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weight_kg",
                schema: "master",
                table: "items",
                type: "decimal(10,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "volume_m3",
                schema: "master",
                table: "items");

            migrationBuilder.DropColumn(
                name: "weight_kg",
                schema: "master",
                table: "items");
        }
    }
}
