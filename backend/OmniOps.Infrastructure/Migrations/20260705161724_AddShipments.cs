using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OmniOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    BatchNumber = table.Column<string>(type: "text", nullable: false),
                    MinSafeTempCelsius = table.Column<double>(type: "double precision", nullable: false),
                    MaxSafeTempCelsius = table.Column<double>(type: "double precision", nullable: false),
                    ValueAtRiskUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DepartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedDeliveryUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Shipments",
                columns: new[] { "Id", "BatchNumber", "DepartedAtUtc", "ExpectedDeliveryUtc", "MaxSafeTempCelsius", "MinSafeTempCelsius", "ProductName", "Status", "ValueAtRiskUsd", "VehicleId" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-000000000001"), "B-4471", new DateTime(2026, 7, 1, 8, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 7, 18, 0, 0, 0, DateTimeKind.Utc), 100.0, 50.0, "Insulin Glargine", 0, 12400m, "Truck-001" },
                    { new Guid("a2000000-0000-0000-0000-000000000002"), "HBV-0293", new DateTime(2026, 7, 2, 6, 30, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 8, 14, 0, 0, 0, DateTimeKind.Utc), 95.0, 50.0, "Hepatitis B Vaccine", 0, 8750m, "Truck-002" },
                    { new Guid("a3000000-0000-0000-0000-000000000003"), "BCG-1182", new DateTime(2026, 7, 3, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 9, 20, 0, 0, 0, DateTimeKind.Utc), 100.0, 55.0, "BCG Vaccine", 0, 6200m, "Truck-003" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_VehicleId_Status",
                table: "Shipments",
                columns: new[] { "VehicleId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Shipments");
        }
    }
}
