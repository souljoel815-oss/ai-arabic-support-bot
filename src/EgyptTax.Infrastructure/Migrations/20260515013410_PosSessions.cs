using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PosSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pos");

            migrationBuilder.AddColumn<bool>(
                name: "require_pos_session",
                schema: "master",
                table: "companies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "pos_sessions",
                schema: "pos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    opened_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    opened_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    closed_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    expected_closing_cash = table.Column<decimal>(type: "decimal(19,2)", nullable: true),
                    actual_closing_cash = table.Column<decimal>(type: "decimal(19,2)", nullable: true),
                    variance = table.Column<decimal>(type: "decimal(19,2)", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    opening_cash = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_sessions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pos_sessions_opened_at",
                schema: "pos",
                table: "pos_sessions",
                column: "opened_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pos_sessions",
                schema: "pos");

            migrationBuilder.DropColumn(
                name: "require_pos_session",
                schema: "master",
                table: "companies");
        }
    }
}
