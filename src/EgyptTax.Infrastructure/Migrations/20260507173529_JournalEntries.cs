using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class JournalEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "accounting");

            migrationBuilder.CreateTable(
                name: "journal_entries",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    source_document_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    source_document_number = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    source_document_type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    posted_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "journal_entry_lines",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    journal_entry_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    account_code = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    credit = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    debit = table.Column<decimal>(type: "decimal(19,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entry_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalSchema: "accounting",
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_source_document_id",
                schema: "accounting",
                table: "journal_entries",
                column: "source_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_source_document_number",
                schema: "accounting",
                table: "journal_entries",
                column: "source_document_number");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entry_lines_account_code",
                schema: "accounting",
                table: "journal_entry_lines",
                column: "account_code");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entry_lines_journal_entry_id",
                schema: "accounting",
                table: "journal_entry_lines",
                column: "journal_entry_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "journal_entry_lines",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "journal_entries",
                schema: "accounting");
        }
    }
}
