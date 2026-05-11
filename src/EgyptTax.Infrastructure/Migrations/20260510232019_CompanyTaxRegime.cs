using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompanyTaxRegime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default existing rows to Standard so the new NOT NULL
            // column doesn't break installs that pre-date Law 6/2025.
            migrationBuilder.AddColumn<string>(
                name: "tax_regime",
                schema: "master",
                table: "companies",
                type: "varchar(24)",
                unicode: false,
                maxLength: 24,
                nullable: false,
                defaultValue: "Standard");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tax_regime",
                schema: "master",
                table: "companies");
        }
    }
}
