namespace Mercurius.Repo.Repositories
{
    /// <summary>
    /// Collection name constants matching the default LiteDbRepository naming convention
    /// (typeof(T).Name — singular, PascalCase). Use these when querying GetCollection&lt;T&gt;()
    /// directly to ensure you hit the same collection the repository uses.
    /// </summary>
    public static class LiteDbCollectionNames
    {
        public const string Products = "Product";
        public const string Customers = "Customer";
        public const string Invoices = "Invoice";
        public const string InvoiceItems = "InvoiceItem";
        public const string InvoiceRefunds = "InvoiceRefund";
        public const string InvoiceItemRefunds = "InvoiceItemRefund";
        public const string InvoiceStatus = "InvoiceStatus";
        public const string ShipmentArrivals = "ShipmentArrival";
        public const string ShipmentArrivalItems = "ShipmentArrivalItem";
        public const string ShipmentArrivalStatus = "ShipmentArrivalStatus";
        public const string Adjustments = "Adjustment";
        public const string AdjustmentReasons = "AdjustmentReason";
        public const string Suppliers = "Supplier";
        public const string ProductCategories = "ProductCategory";
        public const string Locations = "Location";
        public const string LocationSettings = "LocationSetting";
        public const string Addresses = "Address";
        public const string Colors = "Color";
        public const string Sizes = "Size";
        public const string Branches = "Branch";
        public const string BulkPackages = "BulkPackage";
        public const string ContactInformations = "ContactInformation";
        public const string CustomerContactInformations = "CustomerContactInformation";
        public const string BranchContactInformations = "BranchContactInformation";
        public const string ItemMovements = "ItemMovement";
        public const string Transactions = "Transaction";
        public const string TransactionIdGenerators = "TransactionIdGenerator";
        public const string UserInformations = "UserInformation";
        public const string UserCurrentLocations = "UserCurrentLocation";
        public const string RoleModuleAccess = "RoleModuleAccess";
        public const string ZeroStockSaleAuditLogs = "ZeroStockSaleAuditLog";
    }
}
