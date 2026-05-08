using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FirmPortalAndPeriodReviewLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accountant_firm_users",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    firm_name = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    firm_external_identifier = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    invited_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    invited_by_user_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false
                    ),
                    accepted_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    revoked_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    revoked_by_user_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accountant_firm_users", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_accountant_firm_users_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "period_review_locks",
                schema: "workflow",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    period_year = table.Column<int>(type: "int", nullable: false),
                    period_month = table.Column<int>(type: "int", nullable: false),
                    locked_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    locked_by_user_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false
                    ),
                    locked_note = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                    released_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    released_by_user_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true
                    ),
                    accountant_actions_during_lock = table.Column<int>(
                        type: "int",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_period_review_locks", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_firm_users_external_id",
                schema: "identity",
                table: "accountant_firm_users",
                column: "firm_external_identifier"
            );

            migrationBuilder.CreateIndex(
                name: "ux_period_review_locks_active_year_month",
                schema: "workflow",
                table: "period_review_locks",
                columns: new[] { "period_year", "period_month" },
                unique: true,
                filter: "[released_at_utc] IS NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "accountant_firm_users", schema: "identity");

            migrationBuilder.DropTable(name: "period_review_locks", schema: "workflow");
        }
    }
}
