using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Leads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "crm");

            migrationBuilder.CreateTable(
                name: "leads",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    company_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    phone = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    email = table.Column<string>(type: "varchar(254)", unicode: false, maxLength: 254, nullable: true),
                    source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    stage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    assigned_to_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    expected_close_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expected_value_egp = table.Column<decimal>(type: "decimal(19,2)", nullable: true),
                    next_action = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    next_action_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    won_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    lost_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    lost_reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    converted_to_customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    converted_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    name_ar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leads", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lead_activities",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    lead_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    logged_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_activities", x => x.id);
                    table.ForeignKey(
                        name: "FK_lead_activities_leads_lead_id",
                        column: x => x.lead_id,
                        principalSchema: "crm",
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lead_activities_lead_id",
                schema: "crm",
                table: "lead_activities",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_assigned_to_user_id",
                schema: "crm",
                table: "leads",
                column: "assigned_to_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_stage",
                schema: "crm",
                table: "leads",
                column: "stage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lead_activities",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "leads",
                schema: "crm");
        }
    }
}
