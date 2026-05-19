using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformAuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ActorEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ResourceType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ResourceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AffectedTenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsImpersonationContext = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAuditEvents_ActorUserId",
                table: "PlatformAuditEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAuditEvents_AffectedTenantId",
                table: "PlatformAuditEvents",
                column: "AffectedTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAuditEvents_CreatedAtUtc",
                table: "PlatformAuditEvents",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformAuditEvents");
        }
    }
}
