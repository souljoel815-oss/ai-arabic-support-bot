using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Timesheets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "hourly_rate_egp",
                schema: "identity",
                table: "users",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "timesheet_entries",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    entry_date = table.Column<DateOnly>(type: "date", nullable: false),
                    project_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    project_task_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    hours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    billable = table.Column<bool>(type: "bit", nullable: false),
                    hourly_rate_egp = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_timesheet_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_timesheet_entries_project_id",
                schema: "projects",
                table: "timesheet_entries",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_timesheet_entries_user_date",
                schema: "projects",
                table: "timesheet_entries",
                columns: new[] { "user_id", "entry_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "timesheet_entries",
                schema: "projects");

            migrationBuilder.DropColumn(
                name: "hourly_rate_egp",
                schema: "identity",
                table: "users");
        }
    }
}
