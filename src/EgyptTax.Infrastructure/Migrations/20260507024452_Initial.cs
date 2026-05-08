using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "audit");

            migrationBuilder.CreateTable(
                name: "audit_log",
                schema: "audit",
                columns: table => new
                {
                    index = table.Column<long>(type: "bigint", nullable: false),
                    ts_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    actor_firm_name = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: true
                    ),
                    company_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    kind = table.Column<string>(
                        type: "varchar(64)",
                        unicode: false,
                        maxLength: 64,
                        nullable: false
                    ),
                    payload_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    prev_hash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    this_hash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.index);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_company_ts",
                schema: "audit",
                table: "audit_log",
                columns: new[] { "company_id", "ts_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_ts_utc",
                schema: "audit",
                table: "audit_log",
                column: "ts_utc"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "audit_log", schema: "audit");
        }
    }
}
