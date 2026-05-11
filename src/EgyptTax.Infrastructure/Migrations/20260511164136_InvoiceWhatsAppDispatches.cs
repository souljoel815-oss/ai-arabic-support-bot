using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceWhatsAppDispatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoice_whatsapp_dispatches",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sales_invoice_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    recipient_phone = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    message_body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    sent_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    sent_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    delivery_status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    provider_message_id = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    delivered_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    read_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    failed_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    failure_reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_whatsapp_dispatches", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_whatsapp_dispatches_invoice_sent",
                schema: "documents",
                table: "invoice_whatsapp_dispatches",
                columns: new[] { "sales_invoice_id", "sent_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_whatsapp_dispatches_provider_id",
                schema: "documents",
                table: "invoice_whatsapp_dispatches",
                column: "provider_message_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_whatsapp_dispatches",
                schema: "documents");
        }
    }
}
