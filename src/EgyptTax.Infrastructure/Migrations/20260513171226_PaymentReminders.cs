using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "payment_reminder_days_overdue",
                schema: "settings",
                table: "notification_prefs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "payment_reminder_enabled",
                schema: "settings",
                table: "notification_prefs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "payment_reminder_dispatches",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sent_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    outstanding_at_send_egp = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    oldest_unpaid_invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    sent_to_email = table.Column<string>(type: "varchar(254)", unicode: false, maxLength: 254, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_reminder_dispatches", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_reminder_dispatches_customer_sent",
                schema: "settings",
                table: "payment_reminder_dispatches",
                columns: new[] { "customer_id", "sent_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_reminder_dispatches",
                schema: "settings");

            migrationBuilder.DropColumn(
                name: "payment_reminder_days_overdue",
                schema: "settings",
                table: "notification_prefs");

            migrationBuilder.DropColumn(
                name: "payment_reminder_enabled",
                schema: "settings",
                table: "notification_prefs");
        }
    }
}
