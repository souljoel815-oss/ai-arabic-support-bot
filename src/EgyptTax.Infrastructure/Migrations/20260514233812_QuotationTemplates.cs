using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class QuotationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quotation_templates",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotation_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quotation_template_lines",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quotation_template_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    vat_category_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    vat_rate_percent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(19,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotation_template_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_quotation_template_lines_quotation_templates_quotation_template_id",
                        column: x => x.quotation_template_id,
                        principalSchema: "documents",
                        principalTable: "quotation_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quotation_template_lines_quotation_template_id",
                schema: "documents",
                table: "quotation_template_lines",
                column: "quotation_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotation_templates_is_active",
                schema: "documents",
                table: "quotation_templates",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quotation_template_lines",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "quotation_templates",
                schema: "documents");
        }
    }
}
