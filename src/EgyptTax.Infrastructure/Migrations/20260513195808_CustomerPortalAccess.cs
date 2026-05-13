using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CustomerPortalAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_portal_access",
                schema: "master_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    token = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    last_viewed_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    revoked = table.Column<bool>(type: "bit", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_portal_access", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_portal_access_customer_id",
                schema: "master_data",
                table: "customer_portal_access",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ux_customer_portal_access_token",
                schema: "master_data",
                table: "customer_portal_access",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_portal_access",
                schema: "master_data");
        }
    }
}
