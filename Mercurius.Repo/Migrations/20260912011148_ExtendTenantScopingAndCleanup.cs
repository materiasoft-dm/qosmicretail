using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercurius.Repo.Migrations
{
    /// <inheritdoc />
    public partial class ExtendTenantScopingAndCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchContactInformations");

            migrationBuilder.DropTable(
                name: "RoleModuleAccesses");

            migrationBuilder.DropTable(
                name: "TransactionIdGenerators");

            migrationBuilder.DropTable(
                name: "TransactionItems");

            migrationBuilder.DropTable(
                name: "UserInformations");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ZeroStockSaleAuditLogs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Suppliers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ShipmentArrivals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ShipmentArrivalItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "RefundReasons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PurchaseOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PurchaseOrderItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ProductCategories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "MedicineBatches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "LocationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ItemMovements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "InvoiceRefunds",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "InvoiceItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "InvoiceItemRefunds",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Files",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CustomerContactInformations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ContactInformations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CategoryFields",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "BulkPackages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Adjustments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AdjustmentReasons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Addresses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ZeroStockSaleAuditLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ShipmentArrivals");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ShipmentArrivalItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RefundReasons");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProductCategories");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MedicineBatches");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LocationSettings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ItemMovements");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "InvoiceRefunds");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "InvoiceItemRefunds");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CustomerContactInformations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ContactInformations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CategoryFields");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BulkPackages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Adjustments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AdjustmentReasons");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Addresses");

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AddressId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Branches_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleModuleAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModuleIdentifier = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleModuleAccesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionIdGenerators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CurrentCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Padding = table.Column<int>(type: "INTEGER", nullable: true),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Suffix = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionIdGenerators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    TransactionId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    TransactionBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TransactionStatusId = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_Transactions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserInformations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    IdentityUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInformations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BranchContactInformations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContactInformationId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchContactInformations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchContactInformations_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchContactInformations_ContactInformations_ContactInformationId",
                        column: x => x.ContactInformationId,
                        principalTable: "ContactInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransactionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransactionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CostPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    SalesPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionItems_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "TransactionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BranchContactInformations_BranchId",
                table: "BranchContactInformations",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchContactInformations_ContactInformationId",
                table: "BranchContactInformations",
                column: "ContactInformationId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_AddressId",
                table: "Branches",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionItems_TransactionId",
                table: "TransactionItems",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CustomerId",
                table: "Transactions",
                column: "CustomerId");
        }
    }
}
