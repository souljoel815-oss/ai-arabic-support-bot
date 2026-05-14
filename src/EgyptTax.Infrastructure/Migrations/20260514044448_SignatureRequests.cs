using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SignatureRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "signatures");

            migrationBuilder.CreateTable(
                name: "signature_requests",
                schema: "signatures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    document_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    token = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    expires_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    signed_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    signer_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    signer_ip = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    revoked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signature_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_signature_requests_document",
                schema: "signatures",
                table: "signature_requests",
                columns: new[] { "document_type", "document_id" });

            migrationBuilder.CreateIndex(
                name: "ux_signature_requests_token",
                schema: "signatures",
                table: "signature_requests",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "signature_requests",
                schema: "signatures");
        }
    }
}
