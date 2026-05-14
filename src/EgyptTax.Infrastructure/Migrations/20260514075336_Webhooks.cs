using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Webhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "webhooks",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    url = table.Column<string>(type: "varchar(2000)", unicode: false, maxLength: 2000, nullable: false),
                    secret = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false),
                    event_mask = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    enabled = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    last_dispatch_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    last_dispatch_status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhooks", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "webhooks",
                schema: "settings");
        }
    }
}
