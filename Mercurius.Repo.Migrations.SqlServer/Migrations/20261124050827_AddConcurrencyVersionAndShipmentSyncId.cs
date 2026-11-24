using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercurius.Repo.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyVersionAndShipmentSyncId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "ShipmentArrivals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "MedicineBatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentArrivals_SyncId",
                table: "ShipmentArrivals",
                column: "SyncId",
                unique: true,
                filter: "[SyncId] IS NOT NULL");

            // MercuriusUser.IsActive used to be display-only: sign-in never checked it, and existing
            // databases hold accounts with false that nobody deactivated (including the seed admin in a
            // local copy). Sign-in now refuses an inactive user, so without this every one of those
            // people would be locked out on the first deploy. Everyone is made active
            // except accounts the audit trail shows an administrator deliberately deactivated (the audit
            // interceptor records that as "Deactivated"); those stay inactive, and are now really locked out.
            migrationBuilder.Sql(
                "UPDATE [AspNetUsers] SET [IsActive] = 1 WHERE [IsActive] = 0 AND [Id] NOT IN " +
                "(SELECT [EntityId] FROM [AuditLogs] WHERE [Action] = 'Deactivated' AND [EntityType] = 'MercuriusUser' AND [EntityId] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShipmentArrivals_SyncId",
                table: "ShipmentArrivals");

            migrationBuilder.DropColumn(
                name: "SyncId",
                table: "ShipmentArrivals");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "MedicineBatches");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Invoices");
        }
    }
}
