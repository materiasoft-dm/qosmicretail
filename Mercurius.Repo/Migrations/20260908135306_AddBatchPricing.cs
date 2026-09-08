using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercurius.Repo.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitSalePrice",
                table: "MedicineBatches",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MedicineBatchId",
                table: "InvoiceItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicineBatches_ProductId_IsActive_ReceivedDate",
                table: "MedicineBatches",
                columns: new[] { "ProductId", "IsActive", "ReceivedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_MedicineBatchId",
                table: "InvoiceItems",
                column: "MedicineBatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceItems_MedicineBatches_MedicineBatchId",
                table: "InvoiceItems",
                column: "MedicineBatchId",
                principalTable: "MedicineBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceItems_MedicineBatches_MedicineBatchId",
                table: "InvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_MedicineBatches_ProductId_IsActive_ReceivedDate",
                table: "MedicineBatches");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItems_MedicineBatchId",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "UnitSalePrice",
                table: "MedicineBatches");

            migrationBuilder.DropColumn(
                name: "MedicineBatchId",
                table: "InvoiceItems");
        }
    }
}
