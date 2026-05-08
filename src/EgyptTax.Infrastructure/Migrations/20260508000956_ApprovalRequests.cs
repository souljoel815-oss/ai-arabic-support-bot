using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "approval_requests",
                schema: "workflow",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    approved_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    approved_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    rejected_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    rejected_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    rejection_reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_document_id",
                schema: "workflow",
                table: "approval_requests",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_submitted_at_utc",
                schema: "workflow",
                table: "approval_requests",
                column: "submitted_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_submitted_by_user_id",
                schema: "workflow",
                table: "approval_requests",
                column: "submitted_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_requests",
                schema: "workflow");
        }
    }
}
