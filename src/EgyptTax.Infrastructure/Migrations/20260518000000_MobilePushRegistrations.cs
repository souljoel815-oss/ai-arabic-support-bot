using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations;

/// <inheritdoc />
/// <summary>
/// T025 per specs/009-android-app/tasks.md + data-model.md §5.
///
/// Adds the <c>mobile_push_registrations</c> table — one row per
/// (user, device) pair that the Android shell registers for FCM push
/// delivery. Soft-deleted (RevokedAtUtc set, row kept) on logout per
/// data-model.md §5 lifecycle so the audit trail survives.
///
/// Hand-authored migration (no `dotnet ef migrations add` available
/// during the spec-kit scaffold session); column types + indexes
/// mirror the project's existing SQL-Server-flavoured pattern (e.g.
/// `20260515185222_ItemSerials.cs`). The SQLite portable mode picks
/// up the schema via EnsureCreated against the model snapshot
/// (regenerated next time `dotnet ef migrations add` runs).
/// </summary>
public partial class MobilePushRegistrations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "mobile_push_registrations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                device_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                fcm_token = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                platform = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                app_version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                last_seen_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                revoked_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mobile_push_registrations", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_mobile_push_registrations_user_id",
            table: "mobile_push_registrations",
            column: "user_id");

        // Filtered unique on (user, device) while still active per
        // data-model.md §5. SQLite ignores the filter and behaves as
        // an unconditional unique, which is acceptable because no
        // device will hold two simultaneously-active registrations
        // for the same user in practice.
        migrationBuilder.CreateIndex(
            name: "ux_mobile_push_registrations_user_device",
            table: "mobile_push_registrations",
            columns: new[] { "user_id", "device_id" },
            unique: true,
            filter: "[revoked_at_utc] IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "mobile_push_registrations");
    }
}
