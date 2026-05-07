using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TaxPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tax_periods",
                schema: "workflow",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    period_kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    year = table.Column<int>(type: "int", nullable: false),
                    month_or_quarter = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    locked_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    locked_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    locked_reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    reopened_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    reopened_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    reopened_reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_periods", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_tax_periods_kind_year_month",
                schema: "workflow",
                table: "tax_periods",
                columns: new[] { "period_kind", "year", "month_or_quarter" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tax_periods",
                schema: "workflow");
        }
    }
}
