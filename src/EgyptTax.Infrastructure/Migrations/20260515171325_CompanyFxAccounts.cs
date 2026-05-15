using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompanyFxAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fx_gain_account_code",
                schema: "master",
                table: "companies",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fx_loss_account_code",
                schema: "master",
                table: "companies",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fx_gain_account_code",
                schema: "master",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "fx_loss_account_code",
                schema: "master",
                table: "companies");
        }
    }
}
