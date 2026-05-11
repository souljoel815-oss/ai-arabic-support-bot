using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ItemEtaCodeLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "eta_code_activated_at_utc",
                schema: "master",
                table: "items",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "eta_code_failure_reason",
                schema: "master",
                table: "items",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "eta_code_kind",
                schema: "master",
                table: "items",
                type: "varchar(8)",
                unicode: false,
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "eta_code_requested_at_utc",
                schema: "master",
                table: "items",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "eta_code_status",
                schema: "master",
                table: "items",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_items_eta_code_pending",
                schema: "master",
                table: "items",
                columns: new[] { "eta_code_status", "eta_code_requested_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_items_eta_code_pending",
                schema: "master",
                table: "items");

            migrationBuilder.DropColumn(
                name: "eta_code_activated_at_utc",
                schema: "master",
                table: "items");

            migrationBuilder.DropColumn(
                name: "eta_code_failure_reason",
                schema: "master",
                table: "items");

            migrationBuilder.DropColumn(
                name: "eta_code_kind",
                schema: "master",
                table: "items");

            migrationBuilder.DropColumn(
                name: "eta_code_requested_at_utc",
                schema: "master",
                table: "items");

            migrationBuilder.DropColumn(
                name: "eta_code_status",
                schema: "master",
                table: "items");
        }
    }
}
