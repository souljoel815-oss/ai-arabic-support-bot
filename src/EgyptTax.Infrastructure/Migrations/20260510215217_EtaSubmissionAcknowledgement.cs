using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EtaSubmissionAcknowledgement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "regulator_acknowledged_at_utc",
                schema: "eta",
                table: "eta_submissions",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "regulator_long_uuid",
                schema: "eta",
                table: "eta_submissions",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "regulator_rejected_at_utc",
                schema: "eta",
                table: "eta_submissions",
                type: "datetime2(3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "regulator_acknowledged_at_utc",
                schema: "eta",
                table: "eta_submissions");

            migrationBuilder.DropColumn(
                name: "regulator_long_uuid",
                schema: "eta",
                table: "eta_submissions");

            migrationBuilder.DropColumn(
                name: "regulator_rejected_at_utc",
                schema: "eta",
                table: "eta_submissions");
        }
    }
}
