using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class JournalTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "journal_templates",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    schedule = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    auto_reverse = table.Column<bool>(type: "bit", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "journal_template_lines",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    journal_template_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    account_code = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    credit = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    debit = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_template_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_journal_template_lines_journal_templates_journal_template_id",
                        column: x => x.journal_template_id,
                        principalSchema: "accounting",
                        principalTable: "journal_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_journal_template_lines_journal_template_id",
                schema: "accounting",
                table: "journal_template_lines",
                column: "journal_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_templates_is_active",
                schema: "accounting",
                table: "journal_templates",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "journal_template_lines",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "journal_templates",
                schema: "accounting");
        }
    }
}
