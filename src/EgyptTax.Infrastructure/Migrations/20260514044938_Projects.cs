using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Projects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "projects");

            migrationBuilder.CreateTable(
                name: "projects",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    budget_egp = table.Column<decimal>(type: "decimal(19,2)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    name_ar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "project_tasks",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    project_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    assigned_to_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_tasks_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_tasks_project_id",
                schema: "projects",
                table: "project_tasks",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ux_projects_code",
                schema: "projects",
                table: "projects",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_tasks",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "projects",
                schema: "projects");
        }
    }
}
