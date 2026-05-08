using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuditCheckpointAndAppendOnlyTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "audit_meta");

            migrationBuilder.CreateTable(
                name: "checkpoint",
                schema: "audit_meta",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    last_index = table.Column<long>(type: "bigint", nullable: false),
                    last_hash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    ts_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkpoint", x => x.id);
                    table.CheckConstraint("ck_audit_checkpoint_single_row", "[id] = 1");
                }
            );

            // T031 — FR-028 defense-in-depth: append-only trigger that
            // blocks UPDATE/DELETE on [audit].[audit_log] for any
            // connection declaring Application Name=EgyptTaxApp. Other
            // sessions (sa, ad-hoc DBA, test scaffolding) bypass the
            // trigger so forensic mutation remains possible — the
            // hash chain + checkpoint catches any actual tampering.
            migrationBuilder.Sql(
                @"
CREATE TRIGGER [audit].[trg_audit_log_append_only]
ON [audit].[audit_log]
AFTER UPDATE, DELETE
AS
BEGIN
    IF APP_NAME() = N'EgyptTaxApp'
    BEGIN
        RAISERROR('audit.audit_log is append-only; tampering is forbidden for the application account.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END
"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF OBJECT_ID('[audit].[trg_audit_log_append_only]', 'TR') IS NOT NULL DROP TRIGGER [audit].[trg_audit_log_append_only]"
            );

            migrationBuilder.DropTable(name: "checkpoint", schema: "audit_meta");
        }
    }
}
