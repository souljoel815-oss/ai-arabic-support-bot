using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LandedCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "landed_costs",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_date = table.Column<DateOnly>(type: "date", nullable: false),
                    document_number = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    state = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    split_method = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    validated_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    validated_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_landed_costs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "landed_cost_allocations",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    landed_cost_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    purchase_invoice_line_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    allocated_amount = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_landed_cost_allocations", x => x.id);
                    table.ForeignKey(
                        name: "FK_landed_cost_allocations_landed_costs_landed_cost_id",
                        column: x => x.landed_cost_id,
                        principalSchema: "inventory",
                        principalTable: "landed_costs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "landed_cost_lines",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    landed_cost_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    clearing_account_code = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    amount = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_landed_cost_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_landed_cost_lines_landed_costs_landed_cost_id",
                        column: x => x.landed_cost_id,
                        principalSchema: "inventory",
                        principalTable: "landed_costs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_landed_cost_allocations_landed_cost_id",
                schema: "inventory",
                table: "landed_cost_allocations",
                column: "landed_cost_id");

            migrationBuilder.CreateIndex(
                name: "ix_landed_cost_allocations_pi_line_id",
                schema: "inventory",
                table: "landed_cost_allocations",
                column: "purchase_invoice_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_landed_cost_lines_landed_cost_id",
                schema: "inventory",
                table: "landed_cost_lines",
                column: "landed_cost_id");

            migrationBuilder.CreateIndex(
                name: "ix_landed_costs_document_date",
                schema: "inventory",
                table: "landed_costs",
                column: "document_date");

            migrationBuilder.CreateIndex(
                name: "ix_landed_costs_document_number",
                schema: "inventory",
                table: "landed_costs",
                column: "document_number",
                filter: "[document_number] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "landed_cost_allocations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "landed_cost_lines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "landed_costs",
                schema: "inventory");
        }
    }
}
