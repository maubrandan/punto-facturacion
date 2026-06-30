using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalBuyerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AuthorizedAmount",
                table: "FiscalDocuments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuyerName",
                table: "FiscalDocuments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuyerTaxId",
                table: "FiscalDocuments",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorizedAmount",
                table: "FiscalDocuments");

            migrationBuilder.DropColumn(
                name: "BuyerName",
                table: "FiscalDocuments");

            migrationBuilder.DropColumn(
                name: "BuyerTaxId",
                table: "FiscalDocuments");
        }
    }
}
