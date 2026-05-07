using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SessionsPasswordResetAndAdminRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_recovery_log",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    target_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    target_email = table.Column<string>(type: "varchar(254)", unicode: false, maxLength: 254, nullable: false),
                    recovered_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    machine_name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    operator_identity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    audit_emitted_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_recovery_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    token_hash = table.Column<byte[]>(type: "varbinary(32)", nullable: false),
                    issued_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    redeemed_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    issued_by_admin_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    issued_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    last_activity_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    absolute_expires_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    revoked_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    revocation_reason = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ip_address = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_recovery_log_audit_pending",
                schema: "identity",
                table: "admin_recovery_log",
                column: "audit_emitted_at_utc",
                filter: "[audit_emitted_at_utc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_user_id",
                schema: "identity",
                table: "password_reset_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_password_reset_tokens_hash",
                schema: "identity",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sessions_user_id",
                schema: "identity",
                table: "sessions",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_recovery_log",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "password_reset_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "sessions",
                schema: "identity");
        }
    }
}
