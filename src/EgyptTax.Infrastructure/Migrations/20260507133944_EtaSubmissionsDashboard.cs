using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EtaSubmissionsDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "eta");

            migrationBuilder.CreateTable(
                name: "eta_submissions",
                schema: "eta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sales_invoice_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    submission_uuid = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    last_attempt_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    attempt_count = table.Column<int>(type: "int", nullable: false),
                    error_code = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    error_message = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    submission_window_expires_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eta_submissions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_eta_submissions_dashboard",
                schema: "eta",
                table: "eta_submissions",
                columns: new[] { "status", "submission_window_expires_at_utc" })
                .Annotation("SqlServer:Include", new[] { "sales_invoice_id", "attempt_count", "error_code" });

            migrationBuilder.CreateIndex(
                name: "ux_eta_submissions_sales_invoice_id",
                schema: "eta",
                table: "eta_submissions",
                column: "sales_invoice_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eta_submissions",
                schema: "eta");
        }
    }
}
