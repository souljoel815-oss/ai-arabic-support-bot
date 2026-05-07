using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SuppliersAndExpenseCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deductible_expense_categories",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    default_deductible = table.Column<bool>(type: "bit", nullable: false),
                    default_account_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    name_ar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deductible_expense_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    phone = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    email = table.Column<string>(type: "varchar(254)", unicode: false, maxLength: 254, nullable: true),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    last_tin_revalidated_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    address_ar = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    address_en = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    name_ar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    tax_profile_default_purchase_vat_category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    tax_profile_type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    tax_profile_reverse_charge = table.Column<bool>(type: "bit", nullable: false),
                    tax_profile_tin = table.Column<string>(type: "varchar(9)", unicode: false, maxLength: 9, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_deductible_expense_categories_code",
                schema: "master",
                table: "deductible_expense_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_last_tin_revalidated_at_utc",
                schema: "master",
                table: "suppliers",
                column: "last_tin_revalidated_at_utc");

            migrationBuilder.CreateIndex(
                name: "ux_suppliers_code",
                schema: "master",
                table: "suppliers",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deductible_expense_categories",
                schema: "master");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "master");
        }
    }
}
