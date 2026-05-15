using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_terms",
                schema: "master_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    net_days = table.Column<int>(type: "int", nullable: false),
                    discount_percent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    discount_window_days = table.Column<int>(type: "int", nullable: false),
                    is_default = table.Column<bool>(type: "bit", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    name_ar = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    name_en = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_terms", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_terms_is_active",
                schema: "master_data",
                table: "payment_terms",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_payment_terms_is_default",
                schema: "master_data",
                table: "payment_terms",
                column: "is_default");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_terms",
                schema: "master_data");
        }
    }
}
