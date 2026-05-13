using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AiSettingsAndReceiptScans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_settings",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    enabled = table.Column<bool>(type: "bit", nullable: false),
                    encrypted_api_key = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    model_name = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    monthly_budget_egp = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "receipt_scans",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    scanned_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    scanned_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    file_name = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    mime_type = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    raw_response_json = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    extracted_vendor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    extracted_date = table.Column<DateOnly>(type: "date", nullable: true),
                    extracted_total_egp = table.Column<decimal>(type: "decimal(19,2)", nullable: true),
                    extracted_vat_egp = table.Column<decimal>(type: "decimal(19,2)", nullable: true),
                    extracted_category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    input_tokens = table.Column<int>(type: "int", nullable: false),
                    output_tokens = table.Column<int>(type: "int", nullable: false),
                    error_message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_scans", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_receipt_scans_scanned_at_utc",
                schema: "settings",
                table: "receipt_scans",
                column: "scanned_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_settings",
                schema: "settings");

            migrationBuilder.DropTable(
                name: "receipt_scans",
                schema: "settings");
        }
    }
}
