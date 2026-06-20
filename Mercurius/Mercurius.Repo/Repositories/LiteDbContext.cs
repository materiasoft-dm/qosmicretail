using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Mercurius.Repo.Repositories
{
    /// <summary>
    /// LiteDB database context for managing database connections and providing
    /// access to the underlying LiteDatabase instance.
    /// </summary>
    public class LiteDbContext : IDisposable
    {
        private readonly LiteDatabase _database;
        private readonly HashSet<string> _ensuredIndexes = new();
        private readonly object _ensuredIndexesLock = new();
        private bool _disposed;

        public LiteDbContext(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            // Configure BsonMapper with type converters to handle legacy int-to-decimal migration
            // for Product.CurrentStock and Product.LowStockCount (R11 pharmacy support)
            var mapper = BsonMapper.Global;
            
            // Register converter for int -> decimal to handle legacy data
            mapper.RegisterType(
                deserialize: (bson) =>
                {
                    if (bson.IsInt32) return (decimal)bson.AsInt32;
                    if (bson.IsDouble) return (decimal)bson.AsDouble;
                    if (bson.IsDecimal) return bson.AsDecimal;
                    return 0m;
                },
                serialize: (obj) => Convert.ToDecimal(obj)
            );

            _database = new LiteDatabase(connectionString, mapper);
            EnsureCoreIndexes();
        }

        private void EnsureCoreIndexes()
        {
            // ItemMovement
            EnsureIndex<Models.ItemMovement, string>(m => m.TransactionId, unique: true);

            // Product — queried by PartCode, Name, IsActive, CurrentStock
            EnsureIndex<Models.Product, string>(p => p.PartCode);
            EnsureIndex<Models.Product, string>(p => p.Name);
            EnsureIndex<Models.Product, bool>(p => p.IsActive);
            EnsureIndex<Models.Product, decimal>(p => p.CurrentStock);

            // Invoice — queried by InvoiceNumber, CustomerId, StatusId, InvoiceDate
            EnsureIndex<Models.Invoice, string>(i => i.InvoiceNumber, unique: true);
            EnsureIndex<Models.Invoice, int?>(i => i.CustomerId);
            EnsureIndex<Models.Invoice, int>(i => i.StatusId);
            EnsureIndex<Models.Invoice, DateTime>(i => i.InvoiceDate);

            // Customer — queried by FirstName, LastName
            EnsureIndex<Models.Customer, string>(c => c.FirstName);
            EnsureIndex<Models.Customer, string>(c => c.LastName);

            // InvoiceItem — queried by ProductId, InvoiceId
            EnsureIndex<Models.InvoiceItem, int>(ii => ii.ProductId);
            EnsureIndex<Models.InvoiceItem, int>(ii => ii.InvoiceId);
            EnsureIndex<Models.InvoiceItem, int>(ii => ii.StatusId);

            // MedicineBatch — queried by ProductId, ExpiryDate (FEFO), BatchNumber
            EnsureIndex<Models.MedicineBatch, int>(mb => mb.ProductId);
            EnsureIndex<Models.MedicineBatch, DateTime>(mb => mb.ExpiryDate);
            EnsureIndex<Models.MedicineBatch, string>(mb => mb.BatchNumber);

            // CategoryField — queried by CategoryId
            EnsureIndex<Models.CategoryField, int>(cf => cf.CategoryId);

            // DosageForm — lookup by Name
            EnsureIndex<Models.DosageForm, string>(df => df.Name);

            // Doctor � queried by LastName, LicenseNumber
            EnsureIndex<Models.Doctor, string>(d => d.LastName);
            EnsureIndex<Models.Doctor, string>(d => d.LicenseNumber);

            // Prescription � queried by PrescriptionNumber, DoctorId, PatientId
            EnsureIndex<Models.Prescription, string>(p => p.PrescriptionNumber, unique: true);
            EnsureIndex<Models.Prescription, int>(p => p.DoctorId);
            EnsureIndex<Models.Prescription, int>(p => p.PatientId);

            // PrescriptionItem � queried by PrescriptionId, ProductId
            EnsureIndex<Models.PrescriptionItem, int>(pi => pi.PrescriptionId);
            EnsureIndex<Models.PrescriptionItem, int>(pi => pi.ProductId);

            // PurchaseOrder � queried by SupplierId, Status, OrderNumber
            EnsureIndex<Models.PurchaseOrder, int>(po => po.SupplierId);
            EnsureIndex<Models.PurchaseOrder, string>(po => po.Status);
            EnsureIndex<Models.PurchaseOrder, string>(po => po.OrderNumber);

            // PurchaseOrderItem � queried by PurchaseOrderId, ProductId
            EnsureIndex<Models.PurchaseOrderItem, int>(poi => poi.PurchaseOrderId);
            EnsureIndex<Models.PurchaseOrderItem, int>(poi => poi.ProductId);

            // ShipmentArrival � queried by PurchaseOrderId
            EnsureIndex<Models.ShipmentArrival, int?>(sa => sa.PurchaseOrderId);

            // ProductField — queried by ProductId + CategoryFieldId
            EnsureIndex<Models.ProductField, int>(pf => pf.ProductId);
            EnsureIndex<Models.ProductField, int>(pf => pf.CategoryFieldId);

            // Identity stores — indexes used to live in LiteDbUserStore/LiteDbRoleStore ctors,
            // but those run per-request and raced with Connection=shared cookie-validation. Do
            // them once here at startup instead. Use named collections matching the stores.
            _database.GetCollection<IdentityModel.MercuriusUser>("identity_users")
                .EnsureIndex(u => u.NormalizedUserName, unique: true);
            _database.GetCollection<IdentityModel.MercuriusUser>("identity_users")
                .EnsureIndex(u => u.NormalizedEmail);
            _database.GetCollection<Microsoft.AspNetCore.Identity.IdentityRole>("identity_roles")
                .EnsureIndex(r => r.NormalizedName, unique: true);

            // ZeroStockSaleAuditLog — queried by ProductId, InvoiceId, SaleDate
            EnsureIndex<Models.ZeroStockSaleAuditLog, int>(z => z.ProductId);
            EnsureIndex<Models.ZeroStockSaleAuditLog, int>(z => z.InvoiceId);
            EnsureIndex<Models.ZeroStockSaleAuditLog, DateTime>(z => z.SaleDate);
        }

        public ILiteDatabase Database => _database;

        /// <summary>
        /// Gets a collection for the specified entity type.
        /// </summary>
        public ILiteCollection<T> GetCollection<T>(string? name = null) where T : class
        {
            return _database.GetCollection<T>(name ?? typeof(T).Name);
        }

        /// <summary>
        /// Creates a new Unit of Work instance for transaction management.
        /// </summary>
        public IUnitOfWork CreateUnitOfWork()
        {
            return new LiteDbUnitOfWork(_database);
        }

        /// <summary>
        /// Ensures an index exists on the specified collection.
        /// </summary>
        public void EnsureIndex<T, K>(Expression<Func<T, K>> property, bool unique = false) where T : class
        {
            var key = $"{typeof(T).FullName}|{property}|{unique}";
            lock (_ensuredIndexesLock)
            {
                if (!_ensuredIndexes.Add(key)) return;
            }
            var collection = GetCollection<T>();
            collection.EnsureIndex(property, unique);
        }

        /// <summary>
        /// Checks if a collection exists in the database.
        /// </summary>
        public bool CollectionExists(string name)
        {
            return _database.CollectionExists(name);
        }

        /// <summary>
        /// Gets collection names in the database.
        /// </summary>
        public IEnumerable<string> GetCollectionNames()
        {
            return _database.GetCollectionNames();
        }

        /// <summary>
        /// Performs a checkpoint to ensure all data is written to disk.
        /// </summary>
        public void Checkpoint()
        {
            _database.Checkpoint();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _database?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
