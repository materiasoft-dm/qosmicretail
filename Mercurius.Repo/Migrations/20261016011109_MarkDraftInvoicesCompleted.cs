using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercurius.Repo.Migrations
{
    /// <inheritdoc />
    public partial class MarkDraftInvoicesCompleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every sale used to be saved as Draft (status 1) even once fully paid, and nothing ever
            // moved it on, so "Draft" really meant "a normal sale". New sales are now saved as
            // Completed (status 2); this relabels the existing ones to match. Refunded (4) and
            // Deleted (5) invoices are untouched. Status rows come from StatusCollection.InvoiceStatus
            // (seeded at startup), so Completed already exists wherever there are invoices.
            migrationBuilder.Sql("UPDATE InvoiceItems SET StatusId = 2 WHERE StatusId = 1");
            migrationBuilder.Sql("UPDATE Invoices SET StatusId = 2 WHERE StatusId = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately nothing: after this, genuinely completed sales are also status 2, so
            // there's no way to tell which ones used to be Draft.
        }
    }
}
