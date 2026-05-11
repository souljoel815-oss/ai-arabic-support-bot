using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComplianceObligations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "compliance_obligations",
                schema: "workflow",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    kind = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    period_year = table.Column<int>(type: "int", nullable: false),
                    period_ordinal = table.Column<int>(type: "int", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    filed_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    filing_reference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    proof_attachment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_obligations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_compliance_obligations_status_due",
                schema: "workflow",
                table: "compliance_obligations",
                columns: new[] { "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ux_compliance_obligations_natural_key",
                schema: "workflow",
                table: "compliance_obligations",
                columns: new[] { "kind", "period_year", "period_ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compliance_obligations",
                schema: "workflow");
        }
    }
}
