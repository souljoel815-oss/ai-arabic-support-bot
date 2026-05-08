using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class JournalVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "journal_vouchers",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    source_document_type = table.Column<string>(
                        type: "nvarchar(32)",
                        maxLength: 32,
                        nullable: true
                    ),
                    source_document_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true
                    ),
                    is_auto_generated = table.Column<bool>(type: "bit", nullable: false),
                    reverses_journal_voucher_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true
                    ),
                    created_by_user_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false
                    ),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    narration_ar = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    narration_en = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_vouchers", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "journal_voucher_lines",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    journal_voucher_id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false
                    ),
                    account_code = table.Column<string>(
                        type: "varchar(16)",
                        unicode: false,
                        maxLength: 16,
                        nullable: false
                    ),
                    description = table.Column<string>(
                        type: "nvarchar(256)",
                        maxLength: 256,
                        nullable: false
                    ),
                    credit = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                    debit = table.Column<decimal>(type: "decimal(19,2)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_voucher_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_journal_voucher_lines_journal_vouchers_journal_voucher_id",
                        column: x => x.journal_voucher_id,
                        principalSchema: "accounting",
                        principalTable: "journal_vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_journal_voucher_lines_account_code",
                schema: "accounting",
                table: "journal_voucher_lines",
                column: "account_code"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journal_voucher_lines_journal_voucher_id",
                schema: "accounting",
                table: "journal_voucher_lines",
                column: "journal_voucher_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journal_vouchers_date",
                schema: "accounting",
                table: "journal_vouchers",
                column: "date"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journal_vouchers_reverses_journal_voucher_id",
                schema: "accounting",
                table: "journal_vouchers",
                column: "reverses_journal_voucher_id",
                filter: "[reverses_journal_voucher_id] IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journal_vouchers_source_document_id",
                schema: "accounting",
                table: "journal_vouchers",
                column: "source_document_id",
                filter: "[source_document_id] IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "journal_voucher_lines", schema: "accounting");

            migrationBuilder.DropTable(name: "journal_vouchers", schema: "accounting");
        }
    }
}
