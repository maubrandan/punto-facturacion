using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandSaaSBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DunningAttemptCount",
                table: "TenantSubscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExternalCustomerId",
                table: "TenantSubscriptions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSubscriptionId",
                table: "TenantSubscriptions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GracePeriodEndsAtUtc",
                table: "TenantSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDunningAtUtc",
                table: "TenantSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PastDueSinceUtc",
                table: "TenantSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubscriptionInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PlanCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BillingCycle = table.Column<int>(type: "int", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    ExternalInvoiceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionInvoices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_CurrentPeriodEndUtc",
                table: "TenantSubscriptions",
                column: "CurrentPeriodEndUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_Status",
                table: "TenantSubscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionInvoices_TenantId_CreatedAtUtc",
                table: "SubscriptionInvoices",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionInvoices_TenantId_InvoiceNumber",
                table: "SubscriptionInvoices",
                columns: new[] { "TenantId", "InvoiceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionInvoices");

            migrationBuilder.DropIndex(
                name: "IX_TenantSubscriptions_CurrentPeriodEndUtc",
                table: "TenantSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_TenantSubscriptions_Status",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "DunningAttemptCount",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "ExternalCustomerId",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "ExternalSubscriptionId",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "GracePeriodEndsAtUtc",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "LastDunningAtUtc",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "PastDueSinceUtc",
                table: "TenantSubscriptions");
        }
    }
}
