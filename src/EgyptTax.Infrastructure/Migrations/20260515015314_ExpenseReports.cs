using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpenseReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "expense_report_id",
                schema: "documents",
                table: "expenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "expense_reports",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    state = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_reports", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expenses_expense_report_id",
                schema: "documents",
                table: "expenses",
                column: "expense_report_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_reports_created_by",
                schema: "documents",
                table: "expense_reports",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_reports_state",
                schema: "documents",
                table: "expense_reports",
                column: "state");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expense_reports",
                schema: "documents");

            migrationBuilder.DropIndex(
                name: "ix_expenses_expense_report_id",
                schema: "documents",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "expense_report_id",
                schema: "documents",
                table: "expenses");
        }
    }
}
