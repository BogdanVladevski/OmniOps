using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitingTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DeliveryArrivalLatitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DeliveryArrivalLongitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryArrivalSource",
                table: "Trips",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryArrivedAtUtc",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryWaitAlertedAtUtc",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OriginLatitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OriginLongitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PickupArrivalLatitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PickupArrivalLongitude",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PickupArrivalSource",
                table: "Trips",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupArrivedAtUtc",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupWaitAlertedAtUtc",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FreeWaitingMinutes",
                table: "BillingProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "BillingProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WaitingRatePerHour",
                table: "BillingProfiles",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryArrivalLatitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DeliveryArrivalLongitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DeliveryArrivalSource",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DeliveryArrivedAtUtc",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DeliveryWaitAlertedAtUtc",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "OriginLatitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "OriginLongitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PickupArrivalLatitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PickupArrivalLongitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PickupArrivalSource",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PickupArrivedAtUtc",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PickupWaitAlertedAtUtc",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "FreeWaitingMinutes",
                table: "BillingProfiles");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "BillingProfiles");

            migrationBuilder.DropColumn(
                name: "WaitingRatePerHour",
                table: "BillingProfiles");
        }
    }
}
