using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercurius.Repo.Migrations
{
    /// <inheritdoc />
    public partial class AddBirPosAccreditation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VatType",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsVatRegistered",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxAddress",
                table: "Locations",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxBusinessName",
                table: "Locations",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxRegisteredName",
                table: "Locations",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tin",
                table: "Locations",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TinBranchCode",
                table: "Locations",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuyerAddress",
                table: "Invoices",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuyerBusinessStyle",
                table: "Invoices",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuyerName",
                table: "Invoices",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuyerTin",
                table: "Invoices",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Invoices",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountName",
                table: "Invoices",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscountTypeId",
                table: "Invoices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JournalHash",
                table: "Invoices",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LessVat",
                table: "Invoices",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PosTerminalId",
                table: "Invoices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptText",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecialDiscountHolderName",
                table: "Invoices",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecialDiscountHolderTin",
                table: "Invoices",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecialDiscountIdNumber",
                table: "Invoices",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpecialDiscountType",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "TerminalInvoiceSequence",
                table: "Invoices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "Invoices",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatExemptSales",
                table: "Invoices",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatableSales",
                table: "Invoices",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ZeroRatedSales",
                table: "Invoices",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "InvoiceItems",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "InvoiceItems",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VatType",
                table: "InvoiceItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DiscountTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ShortLabel = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    ValueKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    IsVatExempt = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresCustomerId = table.Column<bool>(type: "INTEGER", nullable: false),
                    IdLabel = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PosTerminals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: true),
                    MachineIdentificationNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SerialNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PermitToUseNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PermitToUseDateIssued = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastInvoiceSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    LastZCounter = table.Column<int>(type: "INTEGER", nullable: false),
                    AccumulatedGrandTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ResetCounter = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosTerminals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosTerminals_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosZReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PosTerminalId = table.Column<int>(type: "INTEGER", nullable: false),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: true),
                    ZCounter = table.Column<int>(type: "INTEGER", nullable: false),
                    ResetCounter = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadingDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    SalesCount = table.Column<int>(type: "INTEGER", nullable: false),
                    BeginningInvoiceSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    EndingInvoiceSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    GrossSales = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    LessVat = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    SeniorCitizenDiscount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PwdDiscount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    SoloParentDiscount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    NationalAthleteDiscount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    MedalOfValorDiscount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    NetSales = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    VatableSales = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    VatAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    VatExemptSales = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ZeroRatedSales = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CashSales = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CardSales = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PreviousGrandTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    NewGrandTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ReportText = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosZReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosZReadings_PosTerminals_PosTerminalId",
                        column: x => x.PosTerminalId,
                        principalTable: "PosTerminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DiscountTypeId",
                table: "Invoices",
                column: "DiscountTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PosTerminalId_TerminalInvoiceSequence",
                table: "Invoices",
                columns: new[] { "PosTerminalId", "TerminalInvoiceSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscountTypes_TenantId_Name",
                table: "DiscountTypes",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_PosTerminals_LocationId",
                table: "PosTerminals",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PosTerminals_TenantId_DeviceId",
                table: "PosTerminals",
                columns: new[] { "TenantId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosZReadings_PosTerminalId_ZCounter",
                table: "PosZReadings",
                columns: new[] { "PosTerminalId", "ZCounter" });

            migrationBuilder.CreateIndex(
                name: "IX_PosZReadings_SyncId",
                table: "PosZReadings",
                column: "SyncId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_DiscountTypes_DiscountTypeId",
                table: "Invoices",
                column: "DiscountTypeId",
                principalTable: "DiscountTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_PosTerminals_PosTerminalId",
                table: "Invoices",
                column: "PosTerminalId",
                principalTable: "PosTerminals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_DiscountTypes_DiscountTypeId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_PosTerminals_PosTerminalId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "DiscountTypes");

            migrationBuilder.DropTable(
                name: "PosZReadings");

            migrationBuilder.DropTable(
                name: "PosTerminals");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_DiscountTypeId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PosTerminalId_TerminalInvoiceSequence",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VatType",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsVatRegistered",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "TaxAddress",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "TaxBusinessName",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "TaxRegisteredName",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Tin",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "TinBranchCode",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "BuyerAddress",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BuyerBusinessStyle",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BuyerName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BuyerTin",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountTypeId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "JournalHash",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LessVat",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PosTerminalId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ReceiptText",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SpecialDiscountHolderName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SpecialDiscountHolderTin",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SpecialDiscountIdNumber",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SpecialDiscountType",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TerminalInvoiceSequence",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VatExemptSales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VatableSales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ZeroRatedSales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "VatType",
                table: "InvoiceItems");
        }
    }
}
