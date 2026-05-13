using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Quotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quotations",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_date = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_until_date = table.Column<DateOnly>(type: "date", nullable: false),
                    state = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    quotation_number = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    sent_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    accepted_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    rejected_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    expired_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    converted_to_invoice_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    converted_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    customer_tax_profile_snapshot_default_sales_vat_category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    customer_tax_profile_snapshot_type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    customer_tax_profile_snapshot_tin = table.Column<string>(type: "varchar(9)", unicode: false, maxLength: 9, nullable: true),
                    customer_tax_profile_snapshot_vat_exemption = table.Column<bool>(type: "bit", nullable: false),
                    grand_total = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    net_before_vat = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    subtotal = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    vat_total = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quotation_lines",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quotation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    vat_category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    vat_rate_percent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotation_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_quotation_lines_quotations_quotation_id",
                        column: x => x.quotation_id,
                        principalSchema: "documents",
                        principalTable: "quotations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quotation_lines_quotation_id",
                schema: "documents",
                table: "quotation_lines",
                column: "quotation_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotations_customer_id",
                schema: "documents",
                table: "quotations",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotations_quotation_number",
                schema: "documents",
                table: "quotations",
                column: "quotation_number");

            migrationBuilder.CreateIndex(
                name: "ix_quotations_state",
                schema: "documents",
                table: "quotations",
                column: "state");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quotation_lines",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "quotations",
                schema: "documents");
        }
    }
}
