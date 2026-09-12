using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Mercurius.Repo.IdentityModel;
using Mercurius.Repo.Models;

namespace Mercurius.Repo.Repositories
{
    /// <summary>
    /// EF Core / SQLite database context. Replaces LiteDbContext — inherits Identity's
    /// user/role/claim/login/token tables via IdentityDbContext, and adds one DbSet per
    /// business model.
    /// </summary>
    public class MercuriusDbContext : IdentityDbContext<MercuriusUser>
    {
        private readonly ICurrentTenantContext _currentTenantContext;

        public MercuriusDbContext(DbContextOptions<MercuriusDbContext> options, ICurrentTenantContext currentTenantContext) : base(options)
        {
            _currentTenantContext = currentTenantContext;
        }

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<Address> Addresses => Set<Address>();
        public DbSet<Adjustment> Adjustments => Set<Adjustment>();
        public DbSet<AdjustmentReason> AdjustmentReasons => Set<AdjustmentReason>();
        public DbSet<BulkPackage> BulkPackages => Set<BulkPackage>();
        public DbSet<CategoryField> CategoryFields => Set<CategoryField>();
        public DbSet<ContactInformation> ContactInformations => Set<ContactInformation>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<CustomerContactInformation> CustomerContactInformations => Set<CustomerContactInformation>();
        public DbSet<DosageForm> DosageForms => Set<DosageForm>();
        public DbSet<Models.File> Files => Set<Models.File>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
        public DbSet<InvoiceItemRefund> InvoiceItemRefunds => Set<InvoiceItemRefund>();
        public DbSet<InvoiceRefund> InvoiceRefunds => Set<InvoiceRefund>();
        public DbSet<InvoiceStatus> InvoiceStatuses => Set<InvoiceStatus>();
        public DbSet<ItemMovement> ItemMovements => Set<ItemMovement>();
        public DbSet<Location> Locations => Set<Location>();
        public DbSet<LocationSetting> LocationSettings => Set<LocationSetting>();
        public DbSet<MedicineBatch> MedicineBatches => Set<MedicineBatch>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
        public DbSet<ProductField> ProductFields => Set<ProductField>();
        public DbSet<Province> Provinces => Set<Province>();
        public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
        public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
        public DbSet<RefundReason> RefundReasons => Set<RefundReason>();
        public DbSet<ShipmentArrival> ShipmentArrivals => Set<ShipmentArrival>();
        public DbSet<ShipmentArrivalItem> ShipmentArrivalItems => Set<ShipmentArrivalItem>();
        public DbSet<ShipmentArrivalStatus> ShipmentArrivalStatuses => Set<ShipmentArrivalStatus>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<UserCurrentLocation> UserCurrentLocations => Set<UserCurrentLocation>();
        public DbSet<UserDashboardLayout> UserDashboardLayouts => Set<UserDashboardLayout>();
        public DbSet<ZeroStockSaleAuditLog> ZeroStockSaleAuditLogs => Set<ZeroStockSaleAuditLog>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Mirrors LiteDbContext.EnsureCoreIndexes() 1:1.
            builder.Entity<ItemMovement>().HasIndex(m => m.TransactionId).IsUnique();

            builder.Entity<Product>().HasIndex(p => p.ProductCode);
            builder.Entity<Product>().HasIndex(p => p.Name);
            builder.Entity<Product>().HasIndex(p => p.IsActive);
            builder.Entity<Product>().HasIndex(p => p.CurrentStock);
            builder.Entity<Product>().HasIndex(p => p.SyncId).IsUnique();

            builder.Entity<Invoice>().HasIndex(i => i.InvoiceNumber).IsUnique();
            builder.Entity<Invoice>().HasIndex(i => i.CustomerId);
            builder.Entity<Invoice>().HasIndex(i => i.StatusId);
            builder.Entity<Invoice>().HasIndex(i => i.InvoiceDate);
            builder.Entity<Invoice>().HasIndex(i => i.SyncId).IsUnique();

            builder.Entity<Customer>().HasIndex(c => c.FirstName);
            builder.Entity<Customer>().HasIndex(c => c.LastName);

            builder.Entity<InvoiceItem>().HasIndex(ii => ii.ProductId);
            builder.Entity<InvoiceItem>().HasIndex(ii => ii.InvoiceId);
            builder.Entity<InvoiceItem>().HasIndex(ii => ii.StatusId);
            builder.Entity<InvoiceItem>().HasIndex(ii => ii.SyncId).IsUnique();
            builder.Entity<InvoiceItem>().HasIndex(ii => ii.MedicineBatchId);

            builder.Entity<MedicineBatch>().HasIndex(mb => new { mb.ProductId, mb.IsActive, mb.ReceivedDate });

            builder.Entity<MedicineBatch>().HasIndex(mb => mb.ProductId);
            builder.Entity<MedicineBatch>().HasIndex(mb => mb.ExpiryDate);
            builder.Entity<MedicineBatch>().HasIndex(mb => mb.BatchNumber);

            builder.Entity<CategoryField>().HasIndex(cf => cf.CategoryId);

            builder.Entity<DosageForm>().HasIndex(df => df.Name);

            builder.Entity<PurchaseOrder>().HasIndex(po => po.SupplierId);
            builder.Entity<PurchaseOrder>().HasIndex(po => po.Status);
            builder.Entity<PurchaseOrder>().HasIndex(po => po.OrderNumber);

            builder.Entity<PurchaseOrderItem>().HasIndex(poi => poi.PurchaseOrderId);
            builder.Entity<PurchaseOrderItem>().HasIndex(poi => poi.ProductId);

            builder.Entity<ShipmentArrival>().HasIndex(sa => sa.PurchaseOrderId);

            builder.Entity<ProductField>().HasIndex(pf => pf.ProductId);
            builder.Entity<ProductField>().HasIndex(pf => pf.CategoryFieldId);

            builder.Entity<ZeroStockSaleAuditLog>().HasIndex(z => z.ProductId);
            builder.Entity<ZeroStockSaleAuditLog>().HasIndex(z => z.InvoiceId);
            builder.Entity<ZeroStockSaleAuditLog>().HasIndex(z => z.SaleDate);

            builder.Entity<InvoiceItemRefund>().HasIndex(r => r.InvoiceId);
            builder.Entity<InvoiceItemRefund>().HasIndex(r => r.InvoiceItemId);
            builder.Entity<InvoiceItemRefund>().HasIndex(r => r.InvoiceRefundId);
            builder.Entity<InvoiceItemRefund>().HasIndex(r => r.RefundReasonId);
            builder.Entity<InvoiceRefund>().HasIndex(r => r.InvoiceId);
            builder.Entity<Adjustment>().HasIndex(a => a.ProductId);
            builder.Entity<Adjustment>().HasIndex(a => a.ReasonId);
            builder.Entity<Adjustment>().HasIndex(a => a.InvoiceItemRefundId);

            // The models declare many optional text fields (Description, Note, ImageFilename,
            // etc.) as non-nullable `string` for convenience under <Nullable>enable</Nullable>,
            // without a [Required] attribute — Program.cs already disables ASP.NET Core's
            // implicit-required-from-non-nullable-reference-type behavior for the same reason
            // (see SuppressImplicitRequiredAttributeForNonNullableReferenceTypes). EF Core has
            // its own, separate convention that infers NOT NULL columns from that same C#
            // annotation; left alone it would enforce columns the app never intended to require.
            // Make the database layer follow the same "only actual [Required] is required" rule.
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType != typeof(string)) continue;
                    if (property.IsKey()) continue; // key properties (e.g. IdentityUser.Id) can't be nullable
                    var isExplicitlyRequired = property.PropertyInfo?.GetCustomAttributes(typeof(RequiredAttribute), true).Any() == true;
                    if (!isExplicitlyRequired)
                    {
                        property.IsNullable = true;
                    }
                }
            }

            // EF Core's convention cascade-deletes on every required (non-nullable) FK. SQL
            // Server refuses to create a schema where two such cascade paths could reach the
            // same table (e.g. deleting an InvoiceStatus would cascade to Invoice AND, via
            // Invoice's own cascade to InvoiceItem, plus InvoiceItem's own StatusId FK, reach
            // InvoiceItem twice) — "may cause cycles or multiple cascade paths". SQLite has no
            // such restriction, which is why this never surfaced there. Restricting deletes
            // globally also matches the business intent better anyway: deleting a lookup row
            // like an InvoiceStatus should never silently cascade-delete real invoices.
            foreach (var foreignKey in builder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }

            // SQL Server requires an explicit precision/scale for decimal columns (SQLite is
            // untyped and never needed one). Without this, EF Core warns on every decimal
            // property and silently defaults to decimal(18,2) anyway — set it explicitly, with
            // scale 4 rather than 2, since CurrentStock/LowStockCount carry fractional pharmacy
            // quantities (e.g. 10.5 mL) that 2 decimal places would still handle, but this
            // leaves headroom without meaningfully changing storage size.
            foreach (var property in builder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    property.SetPrecision(18);
                    property.SetScale(4);
                }
            }

            // Tenant isolation, applied once here rather than per-controller — see
            // MULTITENANCY_ARCHITECTURE.md §4.1. Every entity implementing ITenantScoped gets a
            // global query filter comparing its TenantId column against the current request's
            // tenant, resolved through _currentTenantContext (a captured instance field, so EF
            // re-evaluates it per query rather than baking a value into the cached model — the
            // standard pattern for a per-request value inside a query filter). This is the same
            // "loop over GetEntityTypes(), act generically via reflection" shape already used
            // above for the nullable-string, cascade-restrict, and decimal-precision conventions.
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType)) continue;
                SetTenantQueryFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, new object[] { builder });
            }
        }

        private static readonly MethodInfo SetTenantQueryFilterMethod =
            typeof(MercuriusDbContext).GetMethod(nameof(SetTenantQueryFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        private void SetTenantQueryFilter<TEntity>(ModelBuilder builder) where TEntity : class, ITenantScoped
        {
            builder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == _currentTenantContext.TenantId);
        }
    }
}
