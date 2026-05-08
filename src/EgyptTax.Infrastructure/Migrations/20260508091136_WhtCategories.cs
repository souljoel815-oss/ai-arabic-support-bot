using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WhtCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wht_categories",
                schema: "tax",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(
                        type: "varchar(32)",
                        unicode: false,
                        maxLength: 32,
                        nullable: false
                    ),
                    rate_percent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    effective_from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to_date = table.Column<DateOnly>(type: "date", nullable: true),
                    applicable_to = table.Column<string>(
                        type: "nvarchar(24)",
                        maxLength: 24,
                        nullable: false
                    ),
                    name_ar = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    name_en = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wht_categories", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_wht_categories_code",
                schema: "tax",
                table: "wht_categories",
                column: "code"
            );

            migrationBuilder.CreateIndex(
                name: "ix_wht_categories_code_effective_from",
                schema: "tax",
                table: "wht_categories",
                columns: new[] { "code", "effective_from_date" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "wht_categories", schema: "tax");
        }
    }
}
