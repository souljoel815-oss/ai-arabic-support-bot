using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RouteVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "route_visits",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rep_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    visit_date = table.Column<DateOnly>(type: "date", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sequence = table.Column<int>(type: "int", nullable: false),
                    kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    planned_note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    visit_note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_route_visits", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_route_visits_customer_id",
                schema: "documents",
                table: "route_visits",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_route_visits_rep_date_seq",
                schema: "documents",
                table: "route_visits",
                columns: new[] { "rep_user_id", "visit_date", "sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "route_visits",
                schema: "documents");
        }
    }
}
