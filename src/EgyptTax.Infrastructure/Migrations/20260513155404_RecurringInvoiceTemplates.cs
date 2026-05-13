using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecurringInvoiceTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recurring_invoice_templates",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    interval = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    next_run_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    last_generated_date = table.Column<DateOnly>(type: "date", nullable: true),
                    invoices_generated_count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_invoice_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recurring_invoice_template_lines",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    recurring_invoice_template_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(19,3)", nullable: false),
                    vat_category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    vat_rate_percent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_invoice_template_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_invoice_template_lines_recurring_invoice_templates_recurring_invoice_template_id",
                        column: x => x.recurring_invoice_template_id,
                        principalSchema: "documents",
                        principalTable: "recurring_invoice_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_invoice_template_lines_recurring_invoice_template_id",
                schema: "documents",
                table: "recurring_invoice_template_lines",
                column: "recurring_invoice_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_invoice_templates_active_next_run",
                schema: "documents",
                table: "recurring_invoice_templates",
                columns: new[] { "is_active", "next_run_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recurring_invoice_template_lines",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "recurring_invoice_templates",
                schema: "documents");
        }
    }
}
