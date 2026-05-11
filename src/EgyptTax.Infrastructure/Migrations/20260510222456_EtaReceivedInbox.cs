using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EtaReceivedInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eta_received_documents",
                schema: "eta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    regulator_long_uuid = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    supplier_tin = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    supplier_legal_name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    document_number = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    document_date = table.Column<DateOnly>(type: "date", nullable: false),
                    net_before_vat_egp = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    vat_total_egp = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    grand_total_egp = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    first_seen_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    resolved_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    imported_as_purchase_invoice_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eta_received_documents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_eta_received_documents_inbox",
                schema: "eta",
                table: "eta_received_documents",
                columns: new[] { "status", "first_seen_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_eta_received_documents_long_uuid",
                schema: "eta",
                table: "eta_received_documents",
                column: "regulator_long_uuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eta_received_documents",
                schema: "eta");
        }
    }
}
