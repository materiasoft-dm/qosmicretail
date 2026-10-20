using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercurius.Repo.Migrations.SqlServer.Migrations
{
    /// <summary>
    /// Gives every existing tenant the statutory discounts (SC, PWD, Solo Parent, National
    /// Athlete/Coach, Medal of Valor) as editable DiscountType rows. Idempotent: a tenant that
    /// already has a discount type in a category is skipped for that category, so re-running it
    /// (or running it after an admin added their own) never duplicates anything. New tenants get
    /// the same rows from TenantProvisioningService. Mirrors Mercurius.Shared.Bir.DefaultDiscounts,
    /// hard-coded here so the migration doesn't change if those defaults do.
    /// </summary>
    public partial class SeedDefaultDiscountTypes : Migration
    {
        private static readonly (int Category, string Name, string ShortLabel, int Value, bool IsVatExempt, string IdLabel)[] Defaults =
        {
            (1, "Senior Citizen", "SC", 20, true, "OSCA/SC ID No."),
            (2, "Person with Disability", "PWD", 20, true, "PWD ID No."),
            (3, "Solo Parent", "SP", 10, true, "Solo Parent ID No."),
            (4, "National Athlete/Coach", "NAAC", 20, true, "PNSTM ID No."),
            (5, "Medal of Valor", "MOV", 20, false, "MOV ID No."),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var d in Defaults)
            {
                migrationBuilder.Sql($@"
INSERT INTO DiscountTypes (TenantId, Name, ShortLabel, ValueKind, Value, IsVatExempt, RequiresCustomerId, IdLabel, Category, IsActive, CreatedDate, UpdatedDate)
SELECT t.Id, '{d.Name}', '{d.ShortLabel}', 0, {d.Value}, {(d.IsVatExempt ? 1 : 0)}, 1, '{d.IdLabel}', {d.Category}, 1, GETUTCDATE(), GETUTCDATE()
FROM Tenants t
WHERE NOT EXISTS (SELECT 1 FROM DiscountTypes x WHERE x.TenantId = t.Id AND x.Category = {d.Category});");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing: by now invoices may reference these rows, and admins may have edited them.
        }
    }
}
