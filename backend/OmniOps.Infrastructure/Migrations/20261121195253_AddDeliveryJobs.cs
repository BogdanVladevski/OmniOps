using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Trips",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DeliveredLatitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DeliveredLongitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryNote",
                table: "Trips",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DestinationLatitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DestinationLongitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FleetId",
                table: "Trips",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<double>(
                name: "LastLatitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastLongitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPositionAtUtc",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastSpeedKmh",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Trips",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedDeliveryUtc",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientName",
                table: "Trips",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "Trips",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingToken",
                table: "Trips",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Existing trips inherit their owner from the vehicle's fleet.
            migrationBuilder.Sql(@"
                UPDATE ""Trips"" t
                SET ""FleetId"" = v.""FleetId"",
                    ""OrganizationId"" = f.""OrganizationId"",
                    ""CreatedAtUtc"" = COALESCE(t.""StartedAtUtc"", now())
                FROM ""Vehicles"" v
                JOIN ""Fleets"" f ON f.""Id"" = v.""FleetId""
                WHERE v.""Id"" = t.""VehicleId"";");

            migrationBuilder.AlterColumn<double>(
                name: "FuelLevel",
                table: "Telemetries",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<double>(
                name: "EngineTemperature",
                table: "Telemetries",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.CreateTable(
                name: "TripDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripDocuments_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_OrganizationId_DriverId_Status",
                table: "Trips",
                columns: new[] { "OrganizationId", "DriverId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_OrganizationId_Status",
                table: "Trips",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TrackingToken",
                table: "Trips",
                column: "TrackingToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripDocuments_OrganizationId_TripId",
                table: "TripDocuments",
                columns: new[] { "OrganizationId", "TripId" });

            migrationBuilder.CreateIndex(
                name: "IX_TripDocuments_TripId",
                table: "TripDocuments",
                column: "TripId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Trips_OrganizationId_DriverId_Status",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_OrganizationId_Status",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_TrackingToken",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DeliveredLatitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DeliveredLongitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DeliveryNote",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DestinationLatitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DestinationLongitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "FleetId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "LastLatitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "LastLongitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "LastPositionAtUtc",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "LastSpeedKmh",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PlannedDeliveryUtc",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "RecipientName",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "TrackingToken",
                table: "Trips");

            // Phone GPS rows have no fuel/cargo reading; the old schema cannot represent that.
            migrationBuilder.Sql(@"UPDATE ""Telemetries"" SET ""FuelLevel"" = 0 WHERE ""FuelLevel"" IS NULL;");
            migrationBuilder.Sql(@"UPDATE ""Telemetries"" SET ""EngineTemperature"" = 0 WHERE ""EngineTemperature"" IS NULL;");

            migrationBuilder.AlterColumn<double>(
                name: "FuelLevel",
                table: "Telemetries",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "EngineTemperature",
                table: "Telemetries",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);
        }
    }
}
