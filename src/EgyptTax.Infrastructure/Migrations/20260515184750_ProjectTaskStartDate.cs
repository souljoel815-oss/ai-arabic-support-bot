using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProjectTaskStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "start_date",
                schema: "projects",
                table: "project_tasks",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "start_date",
                schema: "projects",
                table: "project_tasks");
        }
    }
}
