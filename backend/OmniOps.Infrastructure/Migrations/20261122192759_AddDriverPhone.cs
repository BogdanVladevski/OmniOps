using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Drivers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Drivers");
        }
    }
}
