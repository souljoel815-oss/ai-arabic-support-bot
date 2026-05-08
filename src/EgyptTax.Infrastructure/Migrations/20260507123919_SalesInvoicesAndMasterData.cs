using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalesInvoicesAndMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "master");

            migrationBuilder.EnsureSchema(name: "documents");

            migrationBuilder.EnsureSchema(name: "tax");

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(
                        type: "varchar(32)",
                        unicode: false,
                        maxLength: 32,
                        nullable: false
                    ),
                    phone = table.Column<string>(
                        type: "varchar(32)",
                        unicode: false,
                        maxLength: 32,
                        nullable: true
                    ),
                    email = table.Column<string>(
                        type: "varchar(254)",
                        unicode: false,
                        maxLength: 254,
                        nullable: true
                    ),
                    status = table.Column<string>(
                        type: "nvarchar(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    address_ar = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    address_en = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    name_ar = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    name_en = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    tax_profile_default_sales_vat_category_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true
                    ),
                    tax_profile_type = table.Column<string>(
                        type: "nvarchar(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    tax_profile_tin = table.Column<string>(
                        type: "varchar(9)",
                        unicode: false,
                        maxLength: 9,
                        nullable: true
                    ),
                    tax_profile_vat_exemption = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "items",
                schema: "master",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(
                        type: "varchar(32)",
                        unicode: false,
                        maxLength: 32,
                        nullable: false
                    ),
                    default_vat_category_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false
                    ),
                    status = table.Column<string>(
                        type: "nvarchar(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    name_ar = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    name_en = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "sales_invoices",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_date = table.Column<DateOnly>(type: "date", nullable: false),
                    state = table.Column<string>(
                        type: "nvarchar(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    document_number = table.Column<string>(
                        type: "varchar(32)",
                        unicode: false,
                        maxLength: 32,
                        nullable: true
                    ),
                    posted_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    posted_by_user_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true
                    ),
                    posting_mode = table.Column<string>(
                        type: "nvarchar(32)",
                        maxLength: 32,
                        nullable: true
                    ),
                    customer_tax_profile_snapshot_default_sales_vat_category_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true
                    ),
                    customer_tax_profile_snapshot_type = table.Column<string>(
                        type: "nvarchar(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    customer_tax_profile_snapshot_tin = table.Column<string>(
                        type: "varchar(9)",
                        unicode: false,
                        maxLength: 9,
                        nullable: true
                    ),
                    customer_tax_profile_snapshot_vat_exemption = table.Column<bool>(
                        type: "bit",
                        nullable: false
                    ),
                    grand_total = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    subtotal = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    vat_total = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoices", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "vat_categories",
                schema: "tax",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(
                        type: "varchar(32)",
                        unicode: false,
                        maxLength: 32,
                        nullable: false
                    ),
                    rate_percent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    effective_from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to_date = table.Column<DateOnly>(type: "date", nullable: true),
                    recoverable_input_vat = table.Column<bool>(type: "bit", nullable: false),
                    name_ar = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    name_en = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vat_categories", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "sales_invoice_lines",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sales_invoice_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false
                    ),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    vat_category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    vat_rate_percent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    line_subtotal = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    line_total = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    line_vat = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoice_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_invoice_lines_sales_invoices_sales_invoice_id",
                        column: x => x.sales_invoice_id,
                        principalSchema: "documents",
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ux_customers_code",
                schema: "master",
                table: "customers",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ux_items_code",
                schema: "master",
                table: "items",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_lines_sales_invoice_id",
                schema: "documents",
                table: "sales_invoice_lines",
                column: "sales_invoice_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_customer_id",
                schema: "documents",
                table: "sales_invoices",
                column: "customer_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoices_document_number",
                schema: "documents",
                table: "sales_invoices",
                column: "document_number"
            );

            migrationBuilder.CreateIndex(
                name: "ux_vat_categories_code_effective_from",
                schema: "tax",
                table: "vat_categories",
                columns: new[] { "code", "effective_from_date" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "customers", schema: "master");

            migrationBuilder.DropTable(name: "items", schema: "master");

            migrationBuilder.DropTable(name: "sales_invoice_lines", schema: "documents");

            migrationBuilder.DropTable(name: "vat_categories", schema: "tax");

            migrationBuilder.DropTable(name: "sales_invoices", schema: "documents");
        }
    }
}
