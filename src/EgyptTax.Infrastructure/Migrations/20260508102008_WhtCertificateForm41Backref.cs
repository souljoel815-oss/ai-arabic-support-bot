using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptTax.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WhtCertificateForm41Backref : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "included_in_form41_filing_id",
                schema: "tax",
                table: "wht_certificates",
                type: "uniqueidentifier",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_wht_certificates_included_in_form41_filing_id",
                schema: "tax",
                table: "wht_certificates",
                column: "included_in_form41_filing_id",
                filter: "[included_in_form41_filing_id] IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_wht_certificates_included_in_form41_filing_id",
                schema: "tax",
                table: "wht_certificates"
            );

            migrationBuilder.DropColumn(
                name: "included_in_form41_filing_id",
                schema: "tax",
                table: "wht_certificates"
            );
        }
    }
}
