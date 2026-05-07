using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NumberingCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "numbering");

            migrationBuilder.CreateTable(
                name: "document_series",
                schema: "numbering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    document_type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    name_ar = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    name_en = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_series", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_number_allocator",
                schema: "numbering",
                columns: table => new
                {
                    series_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    fiscal_year = table.Column<int>(type: "int", nullable: false),
                    next_number = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_number_allocator", x => new { x.series_id, x.fiscal_year });
                    table.ForeignKey(
                        name: "FK_document_number_allocator_document_series_series_id",
                        column: x => x.series_id,
                        principalSchema: "numbering",
                        principalTable: "document_series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_document_series_code",
                schema: "numbering",
                table: "document_series",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_document_series_doc_type",
                schema: "numbering",
                table: "document_series",
                column: "document_type",
                unique: true);

            // Seed one DocumentSeries per DocumentType per FR-011. The
            // GUIDs match DocumentSeriesConfiguration.* deterministic IDs
            // so cross-rebuild references stay stable. Names are
            // bilingual (Arabic + English) per the standard ArabicEnglishText
            // shape; raw SQL is used because EF Core's HasData doesn't
            // seed ComplexProperty members cleanly.
            migrationBuilder.Sql(@"
INSERT INTO [numbering].[document_series] ([id], [code], [document_type], [name_ar], [name_en]) VALUES
    ('11111111-1111-4111-8111-000000000001', 'INV', 'SalesInvoice',           N'فاتورة مبيعات',        N'Sales invoice'),
    ('11111111-1111-4111-8111-000000000002', 'CN',  'CreditNote',             N'إشعار خصم',            N'Credit note'),
    ('11111111-1111-4111-8111-000000000003', 'PI',  'PurchaseInvoice',        N'فاتورة مشتريات',       N'Purchase invoice'),
    ('11111111-1111-4111-8111-000000000004', 'EXP', 'Expense',                N'مصروف',                N'Expense'),
    ('11111111-1111-4111-8111-000000000005', 'JV',  'JournalVoucher',         N'قيد محاسبي',           N'Journal voucher'),
    ('11111111-1111-4111-8111-000000000006', 'SPV', 'SupplierPaymentVoucher', N'إيصال دفع لمورد',      N'Supplier payment voucher'),
    ('11111111-1111-4111-8111-000000000007', 'CRV', 'CustomerReceiptVoucher', N'إيصال قبض من عميل',    N'Customer receipt voucher'),
    ('11111111-1111-4111-8111-000000000008', 'FA',  'FixedAsset',             N'أصل ثابت',             N'Fixed asset');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_number_allocator",
                schema: "numbering");

            migrationBuilder.DropTable(
                name: "document_series",
                schema: "numbering");
        }
    }
}
