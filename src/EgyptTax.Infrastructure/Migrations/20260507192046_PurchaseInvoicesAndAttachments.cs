using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseInvoicesAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attachments",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_type = table.Column<string>(
                        type: "nvarchar(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    filename_original = table.Column<string>(
                        type: "nvarchar(260)",
                        maxLength: 260,
                        nullable: false
                    ),
                    filename_storage = table.Column<string>(
                        type: "varchar(260)",
                        unicode: false,
                        maxLength: 260,
                        nullable: false
                    ),
                    relative_path = table.Column<string>(
                        type: "varchar(512)",
                        unicode: false,
                        maxLength: 512,
                        nullable: false
                    ),
                    sha256 = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    mime_type = table.Column<string>(
                        type: "varchar(100)",
                        unicode: false,
                        maxLength: 100,
                        nullable: false
                    ),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false
                    ),
                    uploaded_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachments", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "purchase_invoices",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    supplier_invoice_number = table.Column<string>(
                        type: "varchar(64)",
                        unicode: false,
                        maxLength: 64,
                        nullable: false
                    ),
                    date_received = table.Column<DateOnly>(type: "date", nullable: false),
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
                    grand_total = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    subtotal = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    supplier_tax_profile_snapshot_default_purchase_vat_category_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true
                    ),
                    supplier_tax_profile_snapshot_type = table.Column<string>(
                        type: "nvarchar(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    supplier_tax_profile_snapshot_reverse_charge = table.Column<bool>(
                        type: "bit",
                        nullable: false
                    ),
                    supplier_tax_profile_snapshot_tin = table.Column<string>(
                        type: "varchar(9)",
                        unicode: false,
                        maxLength: 9,
                        nullable: true
                    ),
                    vat_total = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoices", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "purchase_invoice_lines",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false
                    ),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    expense_category_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true
                    ),
                    quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    vat_category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    vat_rate_percent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    deductible_flag = table.Column<bool>(type: "bit", nullable: false),
                    line_subtotal = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    line_total = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    line_vat = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoice_lines", x => x.id);
                    table.CheckConstraint(
                        "ck_purchase_invoice_lines_item_xor_expense",
                        "([item_id] IS NOT NULL AND [expense_category_id] IS NULL) OR ([item_id] IS NULL AND [expense_category_id] IS NOT NULL)"
                    );
                    table.ForeignKey(
                        name: "FK_purchase_invoice_lines_purchase_invoices_purchase_invoice_id",
                        column: x => x.purchase_invoice_id,
                        principalSchema: "documents",
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_attachments_document_id_type",
                schema: "documents",
                table: "attachments",
                columns: new[] { "document_id", "document_type" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_lines_purchase_invoice_id",
                schema: "documents",
                table: "purchase_invoice_lines",
                column: "purchase_invoice_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoices_document_number",
                schema: "documents",
                table: "purchase_invoices",
                column: "document_number"
            );

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoices_supplier_dedup",
                schema: "documents",
                table: "purchase_invoices",
                columns: new[] { "supplier_id", "supplier_invoice_number" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoices_supplier_id",
                schema: "documents",
                table: "purchase_invoices",
                column: "supplier_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "attachments", schema: "documents");

            migrationBuilder.DropTable(name: "purchase_invoice_lines", schema: "documents");

            migrationBuilder.DropTable(name: "purchase_invoices", schema: "documents");
        }
    }
}
