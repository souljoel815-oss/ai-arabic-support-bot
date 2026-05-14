using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TaskPriorityAndSubTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "parent_task_id",
                schema: "projects",
                table: "project_tasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "priority",
                schema: "projects",
                table: "project_tasks",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            // v5 A.3 — backfill existing rows to 'Normal' (the
            // C# default). The empty-string defaultValue above is
            // an EF artifact; without this UpdateData call the
            // rows would carry "" which Enum.Parse rejects on
            // read. Provider-agnostic via UpdateData (works on
            // both SQL Server and SQLite — SQLite has no schemas
            // but EF translates the schema arg correctly).
            migrationBuilder.UpdateData(
                schema: "projects",
                table: "project_tasks",
                keyColumn: "priority",
                keyValue: "",
                column: "priority",
                value: "Normal");

            migrationBuilder.CreateIndex(
                name: "ix_project_tasks_parent",
                schema: "projects",
                table: "project_tasks",
                column: "parent_task_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_project_tasks_parent",
                schema: "projects",
                table: "project_tasks");

            migrationBuilder.DropColumn(
                name: "parent_task_id",
                schema: "projects",
                table: "project_tasks");

            migrationBuilder.DropColumn(
                name: "priority",
                schema: "projects",
                table: "project_tasks");
        }
    }
}
