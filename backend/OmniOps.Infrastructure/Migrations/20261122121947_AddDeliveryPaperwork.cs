using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryPaperwork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MaxTempCelsius",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MinTempCelsius",
                table: "Trips",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TemperatureCelsius",
                table: "TripDocuments",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxTempCelsius",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "MinTempCelsius",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "TemperatureCelsius",
                table: "TripDocuments");
        }
    }
}
