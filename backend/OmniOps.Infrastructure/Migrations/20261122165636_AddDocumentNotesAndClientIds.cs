using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentNotesAndClientIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TripDocuments_TripId",
                table: "TripDocuments");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "TripDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "TripDocuments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAtUtc",
                table: "TripDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripDocuments_TripId_ClientId",
                table: "TripDocuments",
                columns: new[] { "TripId", "ClientId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TripDocuments_TripId_ClientId",
                table: "TripDocuments");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "TripDocuments");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "TripDocuments");

            migrationBuilder.DropColumn(
                name: "ReceivedAtUtc",
                table: "TripDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_TripDocuments_TripId",
                table: "TripDocuments",
                column: "TripId");
        }
    }
}
