using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CustomerFiscalPositionFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "fiscal_position_id",
                schema: "master",
                table: "customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_fiscal_position_id",
                schema: "master",
                table: "customers",
                column: "fiscal_position_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_customers_fiscal_position_id",
                schema: "master",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "fiscal_position_id",
                schema: "master",
                table: "customers");
        }
    }
}
