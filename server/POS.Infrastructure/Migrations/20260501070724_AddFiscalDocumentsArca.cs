using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalDocumentsArca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FiscalDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalFiscalDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    PointOfSale = table.Column<int>(type: "int", nullable: false),
                    VoucherNumber = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Cae = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CaeExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuthorizedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    NextRetryAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalDocuments_FiscalDocuments_OriginalFiscalDocumentId",
                        column: x => x.OriginalFiscalDocumentId,
                        principalTable: "FiscalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FiscalDocuments_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantFiscalProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TaxId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PointOfSale = table.Column<int>(type: "int", nullable: false),
                    IsProduction = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CertificateRef = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    PrivateKeyRef = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantFiscalProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalDocuments_OriginalFiscalDocumentId",
                table: "FiscalDocuments",
                column: "OriginalFiscalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalDocuments_SaleId",
                table: "FiscalDocuments",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalDocuments_TenantId_PointOfSale_DocumentType_VoucherNumber",
                table: "FiscalDocuments",
                columns: new[] { "TenantId", "PointOfSale", "DocumentType", "VoucherNumber" },
                unique: true,
                filter: "[VoucherNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalDocuments_TenantId_SaleId_DocumentType",
                table: "FiscalDocuments",
                columns: new[] { "TenantId", "SaleId", "DocumentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalDocuments_TenantId_Status_NextRetryAtUtc",
                table: "FiscalDocuments",
                columns: new[] { "TenantId", "Status", "NextRetryAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantFiscalProfiles_TenantId",
                table: "TenantFiscalProfiles",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiscalDocuments");

            migrationBuilder.DropTable(
                name: "TenantFiscalProfiles");
        }
    }
}
