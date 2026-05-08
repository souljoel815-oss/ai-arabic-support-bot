using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_receipt_vouchers",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    receipt_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_method = table.Column<string>(
                        type: "nvarchar(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    payment_reference = table.Column<string>(
                        type: "nvarchar(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    note = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: true
                    ),
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
                    customer_wht_certificate_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true
                    ),
                    gross_receipt_amount = table.Column<decimal>(
                        type: "decimal(19,2)",
                        nullable: false
                    ),
                    net_cash_received = table.Column<decimal>(
                        type: "decimal(19,2)",
                        nullable: false
                    ),
                    wht_receivable_amount = table.Column<decimal>(
                        type: "decimal(19,2)",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_receipt_vouchers", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "supplier_payment_vouchers",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_method = table.Column<string>(
                        type: "nvarchar(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    payment_reference = table.Column<string>(
                        type: "nvarchar(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    note = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: true
                    ),
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
                    generated_wht_certificate_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true
                    ),
                    gross_payment_amount = table.Column<decimal>(
                        type: "decimal(19,2)",
                        nullable: false
                    ),
                    net_cash_paid = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    wht_payable_amount = table.Column<decimal>(
                        type: "decimal(19,2)",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payment_vouchers", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "payment_allocations",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    supplier_payment_voucher_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true
                    ),
                    customer_receipt_voucher_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true
                    ),
                    target_document_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false
                    ),
                    target_document_type = table.Column<string>(
                        type: "nvarchar(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    allocated_amount = table.Column<decimal>(
                        type: "decimal(19,2)",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_allocations", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_allocations_customer_receipt_vouchers_customer_receipt_voucher_id",
                        column: x => x.customer_receipt_voucher_id,
                        principalSchema: "documents",
                        principalTable: "customer_receipt_vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_payment_allocations_supplier_payment_vouchers_supplier_payment_voucher_id",
                        column: x => x.supplier_payment_voucher_id,
                        principalSchema: "documents",
                        principalTable: "supplier_payment_vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_customer_receipt_vouchers_customer_id",
                schema: "documents",
                table: "customer_receipt_vouchers",
                column: "customer_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_customer_receipt_vouchers_document_number",
                schema: "documents",
                table: "customer_receipt_vouchers",
                column: "document_number",
                filter: "[document_number] IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "ix_customer_receipt_vouchers_receipt_date",
                schema: "documents",
                table: "customer_receipt_vouchers",
                column: "receipt_date"
            );

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocations_customer_receipt_voucher_id",
                schema: "documents",
                table: "payment_allocations",
                column: "customer_receipt_voucher_id",
                filter: "[customer_receipt_voucher_id] IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocations_supplier_payment_voucher_id",
                schema: "documents",
                table: "payment_allocations",
                column: "supplier_payment_voucher_id",
                filter: "[supplier_payment_voucher_id] IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "ix_payment_allocations_target_document_id",
                schema: "documents",
                table: "payment_allocations",
                column: "target_document_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_vouchers_document_number",
                schema: "documents",
                table: "supplier_payment_vouchers",
                column: "document_number",
                filter: "[document_number] IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_vouchers_payment_date",
                schema: "documents",
                table: "supplier_payment_vouchers",
                column: "payment_date"
            );

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_vouchers_supplier_id",
                schema: "documents",
                table: "supplier_payment_vouchers",
                column: "supplier_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "payment_allocations", schema: "documents");

            migrationBuilder.DropTable(name: "customer_receipt_vouchers", schema: "documents");

            migrationBuilder.DropTable(name: "supplier_payment_vouchers", schema: "documents");
        }
    }
}
