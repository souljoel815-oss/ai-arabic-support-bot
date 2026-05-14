using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Currencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "currencies",
                schema: "settings",
                columns: table => new
                {
                    code = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    symbol = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    name_en = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    is_base = table.Column<bool>(type: "bit", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currencies", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "exchange_rates",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    currency_code = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    rate_to_base = table.Column<decimal>(type: "decimal(19,6)", nullable: false),
                    source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchange_rates", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_exchange_rates_currency_date",
                schema: "settings",
                table: "exchange_rates",
                columns: new[] { "currency_code", "effective_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "currencies",
                schema: "settings");

            migrationBuilder.DropTable(
                name: "exchange_rates",
                schema: "settings");
        }
    }
}
