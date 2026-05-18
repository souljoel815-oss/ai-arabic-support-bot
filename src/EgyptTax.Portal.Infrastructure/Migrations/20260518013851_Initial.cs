using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organisation_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    actor_team_member_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    actor_display_name_snapshot = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    verb = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    subject_kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    subject_id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    payload_json = table.Column<string>(type: "TEXT", nullable: true),
                    originating_ip = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customer_organisations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    legal_name_ar = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    legal_name_en = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    tax_registration_number = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    billing_email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    billing_phone = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    billing_address_json = table.Column<string>(type: "TEXT", nullable: true),
                    country_code = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 2, nullable: false),
                    requires_mfa_for_owners = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    soft_deleted_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_organisations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organisation_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organisation_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    team_member_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    invited_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    accepted_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    revoked_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    security_stamp_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organisation_memberships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "portal_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portal_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "team_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    locale_preference = table.Column<string>(type: "TEXT", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_login_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    soft_deleted_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    user_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", nullable: true),
                    security_stamp = table.Column<string>(type: "TEXT", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "TEXT", nullable: true),
                    phone_number = table.Column<string>(type: "TEXT", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    access_failed_count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_members", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "portal_role_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    role_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    claim_type = table.Column<string>(type: "TEXT", nullable: true),
                    claim_value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portal_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_portal_role_claims_portal_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "portal_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_member_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    claim_type = table.Column<string>(type: "TEXT", nullable: true),
                    claim_value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_member_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_team_member_claims_team_members_user_id",
                        column: x => x.user_id,
                        principalTable: "team_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_member_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "TEXT", nullable: false),
                    provider_key = table.Column<string>(type: "TEXT", nullable: false),
                    provider_display_name = table.Column<string>(type: "TEXT", nullable: true),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_member_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_team_member_logins_team_members_user_id",
                        column: x => x.user_id,
                        principalTable: "team_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_member_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    role_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_member_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_team_member_roles_portal_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "portal_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_team_member_roles_team_members_user_id",
                        column: x => x.user_id,
                        principalTable: "team_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_member_tokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    login_provider = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_member_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_team_member_tokens_team_members_user_id",
                        column: x => x.user_id,
                        principalTable: "team_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entries_org_occurred_desc",
                table: "audit_log_entries",
                columns: new[] { "organisation_id", "occurred_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entries_verb",
                table: "audit_log_entries",
                column: "verb");

            migrationBuilder.CreateIndex(
                name: "IX_customer_organisations_billing_email",
                table: "customer_organisations",
                column: "billing_email");

            migrationBuilder.CreateIndex(
                name: "IX_customer_organisations_soft_deleted_at_utc",
                table: "customer_organisations",
                column: "soft_deleted_at_utc",
                filter: "[soft_deleted_at_utc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_organisation_memberships_organisation_id",
                table: "organisation_memberships",
                column: "organisation_id");

            migrationBuilder.CreateIndex(
                name: "IX_organisation_memberships_team_member_id",
                table: "organisation_memberships",
                column: "team_member_id");

            migrationBuilder.CreateIndex(
                name: "UX_organisation_memberships_active_org_member",
                table: "organisation_memberships",
                columns: new[] { "organisation_id", "team_member_id" },
                unique: true,
                filter: "[revoked_at_utc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_portal_role_claims_role_id",
                table: "portal_role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "role_name_index",
                table: "portal_roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_member_claims_user_id",
                table: "team_member_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_member_logins_user_id",
                table: "team_member_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_member_roles_role_id",
                table: "team_member_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "email_index",
                table: "team_members",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "user_name_index",
                table: "team_members",
                column: "normalized_user_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log_entries");

            migrationBuilder.DropTable(
                name: "customer_organisations");

            migrationBuilder.DropTable(
                name: "organisation_memberships");

            migrationBuilder.DropTable(
                name: "portal_role_claims");

            migrationBuilder.DropTable(
                name: "team_member_claims");

            migrationBuilder.DropTable(
                name: "team_member_logins");

            migrationBuilder.DropTable(
                name: "team_member_roles");

            migrationBuilder.DropTable(
                name: "team_member_tokens");

            migrationBuilder.DropTable(
                name: "portal_roles");

            migrationBuilder.DropTable(
                name: "team_members");
        }
    }
}
