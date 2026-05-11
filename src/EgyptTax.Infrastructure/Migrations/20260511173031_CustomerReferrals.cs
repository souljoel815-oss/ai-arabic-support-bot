using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CustomerReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "referrals");

            migrationBuilder.CreateTable(
                name: "customer_referrals",
                schema: "referrals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    referred_contact_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    referred_contact_detail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    invited_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    installed_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    purchased_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    referred_customer_hwid = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    reward_days = table.Column<int>(type: "int", nullable: false),
                    note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_referrals", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_referrals_status",
                schema: "referrals",
                table: "customer_referrals",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_referrals",
                schema: "referrals");
        }
    }
}
