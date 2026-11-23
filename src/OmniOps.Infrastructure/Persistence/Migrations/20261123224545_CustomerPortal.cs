using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CustomerArrivingNoticeAtUtc",
                table: "Jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomerDeliveredNoticeAtUtc",
                table: "Jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomerPickedUpNoticeAtUtc",
                table: "Jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailUpdates",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Customers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PortalCreatedAtUtc",
                table: "Customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortalToken",
                table: "Customers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CustomerId",
                table: "Jobs",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_OrganizationId_CustomerId",
                table: "Jobs",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId",
                table: "Invoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrganizationId_CustomerId",
                table: "Invoices",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PortalToken",
                table: "Customers",
                column: "PortalToken",
                unique: true,
                filter: "\"PortalToken\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Customers_CustomerId",
                table: "Invoices",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_Customers_CustomerId",
                table: "Jobs",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Existing data: every customer name on a job becomes a customer (matched the way the app matches names:
            // trimmed, single spaces, lower case), and jobs and invoices point at their customer.
            migrationBuilder.Sql("""
                INSERT INTO "Customers" ("Id", "OrganizationId", "Name", "NormalizedName", "EmailUpdates", "UpdatedAtUtc")
                SELECT DISTINCT ON (j."OrganizationId", n.name)
                       gen_random_uuid(), j."OrganizationId", btrim(j."CustomerName"), n.name, false, now()
                FROM "Jobs" j
                CROSS JOIN LATERAL (SELECT lower(regexp_replace(btrim(j."CustomerName"), ' +', ' ', 'g')) AS name) n
                WHERE j."CustomerName" IS NOT NULL AND btrim(j."CustomerName") <> ''
                ORDER BY j."OrganizationId", n.name, j."CreatedAtUtc" DESC
                ON CONFLICT ("OrganizationId", "NormalizedName") DO NOTHING;

                UPDATE "Jobs" j SET "CustomerId" = c."Id"
                FROM "Customers" c
                WHERE c."OrganizationId" = j."OrganizationId"
                  AND c."NormalizedName" = lower(regexp_replace(btrim(j."CustomerName"), ' +', ' ', 'g'));

                UPDATE "Invoices" i SET "CustomerId" = j."CustomerId"
                FROM "Jobs" j
                WHERE j."Id" = i."JobId" AND j."CustomerId" IS NOT NULL;

                UPDATE "Invoices" i SET "CustomerId" = c."Id"
                FROM "Customers" c
                WHERE i."CustomerId" IS NULL AND c."OrganizationId" = i."OrganizationId"
                  AND c."NormalizedName" = lower(regexp_replace(btrim(i."CustomerName"), ' +', ' ', 'g'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Customers_CustomerId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_Customers_CustomerId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_CustomerId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_OrganizationId_CustomerId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CustomerId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_OrganizationId_CustomerId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Customers_PortalToken",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CustomerArrivingNoticeAtUtc",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "CustomerDeliveredNoticeAtUtc",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "CustomerPickedUpNoticeAtUtc",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "EmailUpdates",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PortalCreatedAtUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PortalToken",
                table: "Customers");
        }
    }
}
