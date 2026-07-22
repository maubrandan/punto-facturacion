using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryStrategyAndTenantBusinessType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                table: "Tenants",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Kiosco");

            migrationBuilder.Sql(
                """
                UPDATE t
                SET t.BusinessType = COALESCE(
                    (
                        SELECT TOP (1) u.BusinessType
                        FROM AspNetUsers u
                        WHERE u.TenantId = t.Id AND u.AccountKind = 0
                        ORDER BY u.Id
                    ),
                    'Kiosco')
                FROM Tenants t
                WHERE t.BusinessType IS NULL OR t.BusinessType = '' OR t.BusinessType = 'Kiosco';
                """);

            // Re-aplicar rubro real desde usuarios aunque el default haya quedado Kiosco.
            migrationBuilder.Sql(
                """
                UPDATE t
                SET t.BusinessType = u.BusinessType
                FROM Tenants t
                INNER JOIN (
                    SELECT TenantId, BusinessType,
                           ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY Id) AS rn
                    FROM AspNetUsers
                    WHERE AccountKind = 0 AND BusinessType IS NOT NULL AND BusinessType <> ''
                ) u ON u.TenantId = t.Id AND u.rn = 1;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "SaleDetails",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<Guid>(
                name: "StockLotId",
                table: "SaleDetails",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "PurchaseDetails",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpirationSnapshot",
                table: "PurchaseDetails",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LotNumberSnapshot",
                table: "PurchaseDetails",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StockLotId",
                table: "PurchaseDetails",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Stock",
                table: "Products",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "StockLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockLots_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    QuantityDelta = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    QuantityAfter = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    StockLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LotNumberSnapshot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExpirationSnapshot = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockLots_StockLotId",
                        column: x => x.StockLotId,
                        principalTable: "StockLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockLots_ProductId",
                table: "StockLots",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLots_TenantId_ProductId_ExpirationDate",
                table: "StockLots",
                columns: new[] { "TenantId", "ProductId", "ExpirationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockLots_TenantId_ProductId_LotNumber",
                table: "StockLots",
                columns: new[] { "TenantId", "ProductId", "LotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductId",
                table: "StockMovements",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StockLotId",
                table: "StockMovements",
                column: "StockLotId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TenantId_ProductId_CreatedAt",
                table: "StockMovements",
                columns: new[] { "TenantId", "ProductId", "CreatedAt" });

            migrationBuilder.Sql(
                """
                INSERT INTO StockLots (Id, TenantId, ProductId, LotNumber, ExpirationDate, Quantity, CreatedAt)
                SELECT
                    NEWID(),
                    p.TenantId,
                    p.Id,
                    N'DEFAULT',
                    '2099-12-31',
                    p.Stock,
                    SYSUTCDATETIME()
                FROM Products p
                INNER JOIN Tenants t ON t.Id = p.TenantId
                WHERE t.BusinessType = N'Farmacia' AND p.Stock > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "StockLots");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StockLotId",
                table: "SaleDetails");

            migrationBuilder.DropColumn(
                name: "ExpirationSnapshot",
                table: "PurchaseDetails");

            migrationBuilder.DropColumn(
                name: "LotNumberSnapshot",
                table: "PurchaseDetails");

            migrationBuilder.DropColumn(
                name: "StockLotId",
                table: "PurchaseDetails");

            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "SaleDetails",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "PurchaseDetails",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<int>(
                name: "Stock",
                table: "Products",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);
        }
    }
}
