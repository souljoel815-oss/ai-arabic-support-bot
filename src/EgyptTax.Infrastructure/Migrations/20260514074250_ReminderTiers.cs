using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReminderTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tier",
                schema: "settings",
                table: "payment_reminder_dispatches",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Gentle");

            migrationBuilder.AddColumn<int>(
                name: "payment_reminder_days_overdue_final",
                schema: "settings",
                table: "notification_prefs",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "payment_reminder_days_overdue_firm",
                schema: "settings",
                table: "notification_prefs",
                type: "int",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.CreateIndex(
                name: "ix_payment_reminder_dispatches_customer_tier_sent",
                schema: "settings",
                table: "payment_reminder_dispatches",
                columns: new[] { "customer_id", "tier", "sent_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payment_reminder_dispatches_customer_tier_sent",
                schema: "settings",
                table: "payment_reminder_dispatches");

            migrationBuilder.DropColumn(
                name: "tier",
                schema: "settings",
                table: "payment_reminder_dispatches");

            migrationBuilder.DropColumn(
                name: "payment_reminder_days_overdue_final",
                schema: "settings",
                table: "notification_prefs");

            migrationBuilder.DropColumn(
                name: "payment_reminder_days_overdue_firm",
                schema: "settings",
                table: "notification_prefs");
        }
    }
}
