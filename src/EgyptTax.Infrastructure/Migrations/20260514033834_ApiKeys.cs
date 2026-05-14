using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_keys",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    display_prefix = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    key_hash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    last_used_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    revoked = table.Column<bool>(type: "bit", nullable: false),
                    revoked_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_api_keys_key_hash",
                schema: "settings",
                table: "api_keys",
                column: "key_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_keys",
                schema: "settings");
        }
    }
}
