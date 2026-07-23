using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAdjustmentReasonsAndExpiryAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Reason",
                table: "StockMovements",
                newName: "ReasonNote");

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                table: "StockMovements",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            // Ajustes históricos: texto libre → Other + nota (ya migrada desde Reason).
            migrationBuilder.Sql(
                """
                UPDATE StockMovements
                SET ReasonCode = N'Other'
                WHERE [Type] = 3 AND ReasonNote IS NOT NULL AND LTRIM(RTRIM(ReasonNote)) <> N'';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TenantId_CreatedAt",
                table: "StockMovements",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_TenantId_CreatedAt",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                table: "StockMovements");

            migrationBuilder.RenameColumn(
                name: "ReasonNote",
                table: "StockMovements",
                newName: "Reason");
        }
    }
}
