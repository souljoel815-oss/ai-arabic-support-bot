using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompanyAndStructuredCustomerAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address_building_number",
                schema: "master",
                table: "customers",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "address_country",
                schema: "master",
                table: "customers",
                type: "varchar(2)",
                unicode: false,
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "address_governorate",
                schema: "master",
                table: "customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "address_postal_code",
                schema: "master",
                table: "customers",
                type: "varchar(16)",
                unicode: false,
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_region_city",
                schema: "master",
                table: "customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "address_street",
                schema: "master",
                table: "customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "companies",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tax_registration_number = table.Column<string>(type: "varchar(9)", unicode: false, maxLength: 9, nullable: false),
                    commercial_registration_number = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    logo_path = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    fiscal_year_start_month = table.Column<int>(type: "int", nullable: false),
                    default_currency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    default_language = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    taxpayer_activity_code = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    address_building_number = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    address_country = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    address_ar = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    address_en = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    address_governorate = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    address_postal_code = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true),
                    address_region_city = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    address_street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    legal_name_ar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    legal_name_en = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "companies",
                schema: "master");

            migrationBuilder.DropColumn(
                name: "address_building_number",
                schema: "master",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "address_country",
                schema: "master",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "address_governorate",
                schema: "master",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "address_postal_code",
                schema: "master",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "address_region_city",
                schema: "master",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "address_street",
                schema: "master",
                table: "customers");
        }
    }
}
