using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SettingsEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "settings");

            migrationBuilder.CreateTable(
                name: "backup_config",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    auto_backup_enabled = table.Column<bool>(type: "bit", nullable: false),
                    frequency = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    save_path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    retention_count = table.Column<int>(type: "int", nullable: false),
                    last_backup_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    last_backup_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    last_backup_path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_settings",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    template_id = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    language = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    prefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    next_number = table.Column<int>(type: "int", nullable: false),
                    default_payment_terms_days = table.Column<int>(type: "int", nullable: false),
                    footer_notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    show_qr_code = table.Column<bool>(type: "bit", nullable: false),
                    show_logo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_prefs",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tax_deadline_enabled = table.Column<bool>(type: "bit", nullable: false),
                    tax_deadline_days_before = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    license_expiry_enabled = table.Column<bool>(type: "bit", nullable: false),
                    license_expiry_days_before = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    eta_failure_enabled = table.Column<bool>(type: "bit", nullable: false),
                    pending_approvals_enabled = table.Column<bool>(type: "bit", nullable: false),
                    backup_reminder_enabled = table.Column<bool>(type: "bit", nullable: false),
                    backup_reminder_days = table.Column<int>(type: "int", nullable: false),
                    eta_cert_expiry_enabled = table.Column<bool>(type: "bit", nullable: false),
                    eta_cert_expiry_days_before = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    email_notifications_enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_prefs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "smtp_settings",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    send_method = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    server = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    port = table.Column<int>(type: "int", nullable: false),
                    username = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    encrypted_password = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    from_address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    from_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    use_tls = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_smtp_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backup_config",
                schema: "settings");

            migrationBuilder.DropTable(
                name: "invoice_settings",
                schema: "settings");

            migrationBuilder.DropTable(
                name: "notification_prefs",
                schema: "settings");

            migrationBuilder.DropTable(
                name: "smtp_settings",
                schema: "settings");
        }
    }
}
