using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScopeTelemetryAndAuditToTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FleetId",
                table: "Telemetries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            // Past readings get the fleet of their truck, but only where the truck id is unambiguous
            // across all fleets; ambiguous ones stay unassigned (and so hidden) rather than risk mixing.
            migrationBuilder.Sql(@"
                UPDATE ""Telemetries"" t
                SET ""FleetId"" = v.""FleetId""
                FROM ""Vehicles"" v
                WHERE v.""ExternalId"" = t.""VehicleId""
                  AND (SELECT COUNT(*) FROM ""Vehicles"" v2 WHERE v2.""ExternalId"" = t.""VehicleId"") = 1;");

            // Audit entries written before accounts existed all belong to the original (seeded) organization.
            migrationBuilder.Sql(@"
                UPDATE ""AuditLogs"" SET ""OrganizationId"" = '01000000-0000-0000-0000-000000000001'
                WHERE ""OrganizationId"" IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Telemetries_FleetId_VehicleId_Timestamp",
                table: "Telemetries",
                columns: new[] { "FleetId", "VehicleId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OrganizationId_OccurredAtUtc",
                table: "AuditLogs",
                columns: new[] { "OrganizationId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Telemetries_FleetId_VehicleId_Timestamp",
                table: "Telemetries");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_OrganizationId_OccurredAtUtc",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "FleetId",
                table: "Telemetries");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AuditLogs");
        }
    }
}
