using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentFleetId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipments_VehicleId_Status",
                table: "Shipments");

            migrationBuilder.AddColumn<Guid>(
                name: "FleetId",
                table: "Shipments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "Shipments",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000001"),
                column: "FleetId",
                value: new Guid("f1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "Shipments",
                keyColumn: "Id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000002"),
                column: "FleetId",
                value: new Guid("f1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "Shipments",
                keyColumn: "Id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000003"),
                column: "FleetId",
                value: new Guid("f1000000-0000-0000-0000-000000000001"));

            // Backfill non-seed shipments from their vehicle's fleet when the vehicle id is unambiguous.
            // Rows whose vehicle id is unknown or exists in several fleets keep Guid.Empty and are never
            // matched by fleet-scoped lookups (no other tenant's safe range can apply to them).
            migrationBuilder.Sql(@"
UPDATE ""Shipments"" s
SET ""FleetId"" = v.""FleetId""
FROM ""Vehicles"" v
WHERE s.""FleetId"" = '00000000-0000-0000-0000-000000000000'
  AND v.""ExternalId"" = s.""VehicleId""
  AND (SELECT COUNT(*) FROM ""Vehicles"" v2 WHERE v2.""ExternalId"" = s.""VehicleId"") = 1;");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_FleetId_VehicleId_Status",
                table: "Shipments",
                columns: new[] { "FleetId", "VehicleId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipments_FleetId_VehicleId_Status",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "FleetId",
                table: "Shipments");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_VehicleId_Status",
                table: "Shipments",
                columns: new[] { "VehicleId", "Status" });
        }
    }
}
