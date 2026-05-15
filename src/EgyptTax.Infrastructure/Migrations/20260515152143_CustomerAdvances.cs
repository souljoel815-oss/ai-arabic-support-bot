using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CustomerAdvances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_advances",
                schema: "master_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    cash_account_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    received_date = table.Column<DateOnly>(type: "date", nullable: false),
                    quotation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    applied_to_invoice_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    applied_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    amount = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_advances", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_advances_customer_id",
                schema: "master_data",
                table: "customer_advances",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_advances_status",
                schema: "master_data",
                table: "customer_advances",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_advances",
                schema: "master_data");
        }
    }
}
