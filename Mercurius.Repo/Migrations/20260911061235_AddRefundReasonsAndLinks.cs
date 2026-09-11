using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercurius.Repo.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundReasonsAndLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Remarks",
                table: "InvoiceItemRefunds",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "RefundReasonId",
                table: "InvoiceItemRefunds",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "WasRestocked",
                table: "InvoiceItemRefunds",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceItemRefundId",
                table: "Adjustments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RefundReasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundReasons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefunds_InvoiceId",
                table: "InvoiceRefunds",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItemRefunds_InvoiceId",
                table: "InvoiceItemRefunds",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItemRefunds_InvoiceItemId",
                table: "InvoiceItemRefunds",
                column: "InvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItemRefunds_RefundReasonId",
                table: "InvoiceItemRefunds",
                column: "RefundReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Adjustments_InvoiceItemRefundId",
                table: "Adjustments",
                column: "InvoiceItemRefundId");

            migrationBuilder.AddForeignKey(
                name: "FK_Adjustments_InvoiceItemRefunds_InvoiceItemRefundId",
                table: "Adjustments",
                column: "InvoiceItemRefundId",
                principalTable: "InvoiceItemRefunds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceItemRefunds_RefundReasons_RefundReasonId",
                table: "InvoiceItemRefunds",
                column: "RefundReasonId",
                principalTable: "RefundReasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Adjustments_InvoiceItemRefunds_InvoiceItemRefundId",
                table: "Adjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceItemRefunds_RefundReasons_RefundReasonId",
                table: "InvoiceItemRefunds");

            migrationBuilder.DropTable(
                name: "RefundReasons");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceRefunds_InvoiceId",
                table: "InvoiceRefunds");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItemRefunds_InvoiceId",
                table: "InvoiceItemRefunds");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItemRefunds_InvoiceItemId",
                table: "InvoiceItemRefunds");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItemRefunds_RefundReasonId",
                table: "InvoiceItemRefunds");

            migrationBuilder.DropIndex(
                name: "IX_Adjustments_InvoiceItemRefundId",
                table: "Adjustments");

            migrationBuilder.DropColumn(
                name: "RefundReasonId",
                table: "InvoiceItemRefunds");

            migrationBuilder.DropColumn(
                name: "WasRestocked",
                table: "InvoiceItemRefunds");

            migrationBuilder.DropColumn(
                name: "InvoiceItemRefundId",
                table: "Adjustments");

            migrationBuilder.AlterColumn<string>(
                name: "Remarks",
                table: "InvoiceItemRefunds",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
