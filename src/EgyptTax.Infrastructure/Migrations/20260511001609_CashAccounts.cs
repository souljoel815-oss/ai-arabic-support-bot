using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CashAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cash_account_id",
                schema: "documents",
                table: "supplier_payment_vouchers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cash_account_id",
                schema: "documents",
                table: "customer_receipt_vouchers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cash_accounts",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    account_code = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    kind = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    currency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    bank_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    account_number = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    branch_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    iban_or_swift = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    is_default = table.Column<bool>(type: "bit", nullable: false),
                    name_ar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_accounts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_cash_accounts_code",
                schema: "master",
                table: "cash_accounts",
                column: "account_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_cash_accounts_default",
                schema: "master",
                table: "cash_accounts",
                column: "is_default",
                unique: true,
                filter: "[is_default] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_accounts",
                schema: "master");

            migrationBuilder.DropColumn(
                name: "cash_account_id",
                schema: "documents",
                table: "supplier_payment_vouchers");

            migrationBuilder.DropColumn(
                name: "cash_account_id",
                schema: "documents",
                table: "customer_receipt_vouchers");
        }
    }
}
