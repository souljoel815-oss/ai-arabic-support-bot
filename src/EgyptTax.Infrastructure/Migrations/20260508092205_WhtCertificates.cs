using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WhtCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wht_certificates",
                schema: "tax",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    direction = table.Column<string>(
                        type: "nvarchar(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    counterparty_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    source_voucher_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false
                    ),
                    source_invoice_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false
                    ),
                    wht_category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rate_applied_percent = table.Column<decimal>(
                        type: "decimal(5,2)",
                        nullable: false
                    ),
                    certificate_number = table.Column<string>(
                        type: "varchar(64)",
                        unicode: false,
                        maxLength: 64,
                        nullable: false
                    ),
                    issued_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    amount_withheld = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wht_certificates", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_wht_certificates_counterparty_id",
                schema: "tax",
                table: "wht_certificates",
                column: "counterparty_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_wht_certificates_date",
                schema: "tax",
                table: "wht_certificates",
                column: "date"
            );

            migrationBuilder.CreateIndex(
                name: "ix_wht_certificates_source_invoice_id",
                schema: "tax",
                table: "wht_certificates",
                column: "source_invoice_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_wht_certificates_source_voucher_id",
                schema: "tax",
                table: "wht_certificates",
                column: "source_voucher_id"
            );

            migrationBuilder.CreateIndex(
                name: "ux_wht_certificates_outbound_certificate_number",
                schema: "tax",
                table: "wht_certificates",
                column: "certificate_number",
                unique: true,
                filter: "[direction] = N'OutboundToSupplier'"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "wht_certificates", schema: "tax");
        }
    }
}
