using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AiGroqAndMemoryNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ai_memory_notes",
                schema: "identity",
                table: "users",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "chat_provider",
                schema: "settings",
                table: "ai_settings",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Anthropic");

            migrationBuilder.AddColumn<string>(
                name: "encrypted_groq_api_key",
                schema: "settings",
                table: "ai_settings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "groq_model_name",
                schema: "settings",
                table: "ai_settings",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: false,
                defaultValue: "meta-llama/llama-4-scout-17b-16e-instruct");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ai_memory_notes",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "chat_provider",
                schema: "settings",
                table: "ai_settings");

            migrationBuilder.DropColumn(
                name: "encrypted_groq_api_key",
                schema: "settings",
                table: "ai_settings");

            migrationBuilder.DropColumn(
                name: "groq_model_name",
                schema: "settings",
                table: "ai_settings");
        }
    }
}
