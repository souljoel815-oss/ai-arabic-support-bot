using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AiChatLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_chat_logs",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    asked_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    asked_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    question = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    raw_response_json = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    reply = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    open_url = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    open_label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    input_tokens = table.Column<int>(type: "int", nullable: false),
                    output_tokens = table.Column<int>(type: "int", nullable: false),
                    error_message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_chat_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_chat_logs_asked_at_utc",
                schema: "settings",
                table: "ai_chat_logs",
                column: "asked_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_chat_logs",
                schema: "settings");
        }
    }
}
