using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IncomeTaxReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "income_tax_returns",
                schema: "tax",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    regime = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    fiscal_year = table.Column<int>(type: "int", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_rate_percent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    contributing_document_count = table.Column<int>(type: "int", nullable: false),
                    generated_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    generated_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    submission_reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    submitted_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    submitted_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ack_reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ack_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ack_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    deductible_expenses = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    management_pl = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    non_deductible_adjustments = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    revenue = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    tax_due = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    taxable_income = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_income_tax_returns", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_income_tax_returns_period",
                schema: "tax",
                table: "income_tax_returns",
                columns: new[] { "regime", "fiscal_year" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "income_tax_returns",
                schema: "tax");
        }
    }
}
