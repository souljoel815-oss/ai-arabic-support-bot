using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalesTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "sales_team_id",
                schema: "identity",
                table: "users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sales_teams",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    manager_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    monthly_target_egp = table.Column<decimal>(type: "decimal(19,2)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    name_ar = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    name_en = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_teams", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_sales_team_id",
                schema: "identity",
                table: "users",
                column: "sales_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_teams_status",
                schema: "identity",
                table: "sales_teams",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_teams",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "ix_users_sales_team_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "sales_team_id",
                schema: "identity",
                table: "users");
        }
    }
}
