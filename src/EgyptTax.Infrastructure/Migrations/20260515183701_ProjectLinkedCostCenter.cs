using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProjectLinkedCostCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "linked_cost_center_id",
                schema: "projects",
                table: "projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_linked_cost_center_id",
                schema: "projects",
                table: "projects",
                column: "linked_cost_center_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_projects_linked_cost_center_id",
                schema: "projects",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "linked_cost_center_id",
                schema: "projects",
                table: "projects");
        }
    }
}
