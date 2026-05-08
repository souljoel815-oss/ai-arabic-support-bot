using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Form41Filings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "form41_filings",
                schema: "tax",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    fiscal_year = table.Column<int>(type: "int", nullable: false),
                    quarter = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(
                        type: "nvarchar(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    generated_at_utc = table.Column<DateTime>(
                        type: "datetime2(3)",
                        nullable: false
                    ),
                    filed_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    filed_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    pdf_path = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    structured_json_path = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    line_count = table.Column<int>(type: "int", nullable: false),
                    total_wht_payable = table.Column<decimal>(
                        type: "decimal(19,2)",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_form41_filings", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_form41_filings_status",
                schema: "tax",
                table: "form41_filings",
                column: "status"
            );

            migrationBuilder.CreateIndex(
                name: "ux_form41_filings_fiscal_year_quarter",
                schema: "tax",
                table: "form41_filings",
                columns: new[] { "fiscal_year", "quarter" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "form41_filings", schema: "tax");
        }
    }
}
