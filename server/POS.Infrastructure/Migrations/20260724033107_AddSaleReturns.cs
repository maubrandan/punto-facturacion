using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReturnStatus",
                table: "Sales",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SaleReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedByUserName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CashSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FiscalDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleReturns_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalTable: "CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturns_FiscalDocuments_FiscalDocumentId",
                        column: x => x.FiscalDocumentId,
                        principalTable: "FiscalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturns_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleReturnLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SaleReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    StockLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LineNetSubtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitNetPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ProductExtendedDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleReturnLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturnLines_SaleDetails_SaleDetailId",
                        column: x => x.SaleDetailId,
                        principalTable: "SaleDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleReturnLines_SaleReturns_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalTable: "SaleReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaleReturnPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SaleReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleReturnPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleReturnPayments_SaleReturns_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalTable: "SaleReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_ProductId",
                table: "SaleReturnLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_SaleDetailId",
                table: "SaleReturnLines",
                column: "SaleDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_SaleReturnId",
                table: "SaleReturnLines",
                column: "SaleReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnLines_TenantId_SaleReturnId",
                table: "SaleReturnLines",
                columns: new[] { "TenantId", "SaleReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnPayments_SaleReturnId",
                table: "SaleReturnPayments",
                column: "SaleReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturnPayments_TenantId_SaleReturnId",
                table: "SaleReturnPayments",
                columns: new[] { "TenantId", "SaleReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_CashSessionId",
                table: "SaleReturns",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_FiscalDocumentId",
                table: "SaleReturns",
                column: "FiscalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_SaleId",
                table: "SaleReturns",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_TenantId_CashSessionId",
                table: "SaleReturns",
                columns: new[] { "TenantId", "CashSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_TenantId_SaleId",
                table: "SaleReturns",
                columns: new[] { "TenantId", "SaleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleReturnLines");

            migrationBuilder.DropTable(
                name: "SaleReturnPayments");

            migrationBuilder.DropTable(
                name: "SaleReturns");

            migrationBuilder.DropColumn(
                name: "ReturnStatus",
                table: "Sales");
        }
    }
}
