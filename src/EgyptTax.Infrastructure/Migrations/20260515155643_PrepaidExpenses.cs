using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PrepaidExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prepaid_expenses",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    expense_account_code = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    start_month = table.Column<DateOnly>(type: "date", nullable: false),
                    period_count = table.Column<int>(type: "int", nullable: false),
                    periods_recognized = table.Column<int>(type: "int", nullable: false),
                    last_recognized_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    total_amount = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prepaid_expenses", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prepaid_expenses",
                schema: "accounting");
        }
    }
}
