using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantSubscriptions",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PlanCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BillingCycle = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    CurrentPeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentPeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrialEndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelAtPeriodEnd = table.Column<bool>(type: "bit", nullable: false),
                    CanceledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSubscriptions", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_TenantSubscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill: derive PlanCode from existing entitlements (same match rules as TenantPlanPresets).
            migrationBuilder.Sql(
                """
                INSERT INTO TenantSubscriptions (
                    TenantId, PlanCode, Status, BillingCycle, Provider,
                    CurrentPeriodStartUtc, CurrentPeriodEndUtc, TrialEndsAtUtc,
                    CancelAtPeriodEnd, CanceledAtUtc, Notes, CreatedAtUtc, UpdatedAtUtc)
                SELECT
                    t.Id,
                    CASE
                        WHEN e.TenantId IS NOT NULL
                             AND e.MaxProducts = 100 AND e.MaxTenantUsers = 3 AND e.SalesEnabled = 1
                            THEN N'Starter'
                        WHEN e.TenantId IS NOT NULL
                             AND e.MaxProducts = 2000 AND e.MaxTenantUsers = 20 AND e.SalesEnabled = 1
                            THEN N'Pro'
                        WHEN e.TenantId IS NOT NULL
                             AND e.MaxProducts IS NULL AND e.MaxTenantUsers IS NULL AND e.SalesEnabled = 1
                            THEN N'Unlimited'
                        ELSE N'Unlimited'
                    END,
                    1, -- Active
                    0, -- Monthly
                    0, -- Manual
                    t.CreatedAt,
                    DATEADD(month, 1, t.CreatedAt),
                    NULL,
                    0,
                    NULL,
                    NULL,
                    t.CreatedAt,
                    SYSUTCDATETIME()
                FROM Tenants t
                LEFT JOIN TenantEntitlements e ON e.TenantId = t.Id
                WHERE NOT EXISTS (
                    SELECT 1 FROM TenantSubscriptions s WHERE s.TenantId = t.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantSubscriptions");
        }
    }
}
