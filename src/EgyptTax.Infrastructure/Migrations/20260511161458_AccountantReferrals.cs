using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AccountantReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "firm_portal");

            migrationBuilder.CreateTable(
                name: "accountant_referrals",
                schema: "firm_portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    accountant_firm_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    referred_customer_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    referred_customer_hwid = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    license_edition = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    license_annual_price_egp = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    commission_rate_percent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    commission_egp = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    referred_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    earned_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    paid_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    paid_reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accountant_referrals", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accountant_referrals_firm_status",
                schema: "firm_portal",
                table: "accountant_referrals",
                columns: new[] { "accountant_firm_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_accountant_referrals_hwid_unique",
                schema: "firm_portal",
                table: "accountant_referrals",
                column: "referred_customer_hwid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accountant_referrals",
                schema: "firm_portal");
        }
    }
}
