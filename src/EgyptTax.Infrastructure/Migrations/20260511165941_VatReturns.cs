using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VatReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vat_returns",
                schema: "tax",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    period_kind = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    period_year = table.Column<int>(type: "int", nullable: false),
                    period_ordinal = table.Column<int>(type: "int", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
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
                    input_vat_non_recoverable = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    input_vat_recoverable = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    net_payable = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    output_vat = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vat_returns", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vat_returns_period",
                schema: "tax",
                table: "vat_returns",
                columns: new[] { "period_kind", "period_year", "period_ordinal" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vat_returns",
                schema: "tax");
        }
    }
}
