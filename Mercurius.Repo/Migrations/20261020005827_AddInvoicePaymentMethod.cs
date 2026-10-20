using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercurius.Repo.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePaymentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Invoices",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            // Until now the payment method only lived in Notes, which every till wrote as
            // "Paid by card" or "Paid by cash". Invoices with neither stay null (unknown).
            migrationBuilder.Sql("UPDATE Invoices SET PaymentMethod = 'Card' WHERE Notes LIKE '%Paid by card%'");
            migrationBuilder.Sql("UPDATE Invoices SET PaymentMethod = 'Cash' WHERE PaymentMethod IS NULL AND Notes LIKE '%Paid by cash%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Invoices");
        }
    }
}
