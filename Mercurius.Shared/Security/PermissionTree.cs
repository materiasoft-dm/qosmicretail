using System.Collections.Generic;
using System.Linq;
using P = Mercurius.Common.ModuleRegistry.Pages;

namespace Mercurius.Common
{
    // One row of the Roles screen's nested list: either a permission a role can hold, or a heading
    // whose checkbox stands for everything under it.
    public sealed class PermissionNode
    {
        public PermissionNode(string label, string? permission, IReadOnlyList<PermissionNode> children)
        {
            Label = label;
            Permission = permission;
            Children = children;
        }

        public string Label { get; }
        public string? Permission { get; }
        public IReadOnlyList<PermissionNode> Children { get; }

        public IEnumerable<string> Permissions =>
            Permission != null ? new[] { Permission } : Children.SelectMany(c => c.Permissions);
    }

    // How ModuleRegistry's flat permission list is laid out for people. Only the arrangement and
    // wording live here: roles still store the same ModuleRegistry.Pages values. Every entry in
    // ModuleRegistry.Modules must appear exactly once (PermissionTreeTests checks that).
    public static class PermissionTree
    {
        public static IReadOnlyList<PermissionNode> Roots { get; } = new[]
        {
            Group("Dashboard",
                Item("Open the dashboard", P.HOME_PAGE),
                Item("Client app dashboard", P.CLIENT_DASHBOARD),
                Group("Widgets",
                    Item("Daily sales", P.CLIENT_DASHBOARD_DailySalesWidget),
                    Item("Monthly sales", P.CLIENT_DASHBOARD_MonthlySalesWidget),
                    Item("Previous month sales", P.CLIENT_DASHBOARD_PreviousMonthSalesWidget),
                    Item("Monthly target", P.CLIENT_DASHBOARD_MonthlyTargetWidget),
                    Item("Monthly new customers", P.CLIENT_DASHBOARD_MonthlyNewCustomers),
                    Item("Items sold today", P.CLIENT_DASHBOARD_ItemsSoldTodayWidget),
                    Item("Stock warning", P.CLIENT_DASHBOARD_StockWarningWidget),
                    Item("Shortcut links", P.CLIENT_DASHBOARD_ShortcutWidget))),
            Group("Sell",
                Item("Make a sale", P.NEWSALE_CREATE),
                Item("See cost prices", P.NEWSALE_VIEW_COSTPRICE)),
            Group("Invoices",
                Item("View invoice list", P.INVOICE_INDEX),
                Item("Open an invoice", P.INVOICE_VIEW),
                Item("See cost prices", P.INVOICE_VIEW_SHOWCOST),
                Item("Update payment", P.INVOICE_UPDATE_PAYMENT),
                Item("Refund a sale", P.REFUND_CREATE)),
            Group("Wholesale",
                Item("View order list", P.WHOLESALE_INDEX),
                Item("Open an order", P.WHOLESALE_VIEW),
                Item("Create order", P.WHOLESALE_CREATE),
                Item("Record payment", P.WHOLESALE_RECORD_PAYMENT),
                Item("Delete order (not in use yet)", P.WHOLESALE_DELETE)),
            Group("Products",
                Item("View product list", P.PRODUCTS_INDEX),
                Item("See cost prices in the list", P.PRODUCTS_INDEX_SHOW_COST),
                Item("Add product", P.PRODUCTS_CREATE),
                Item("See cost price when adding", P.PRODUCTS_CREATE_SHOW_COST),
                Item("See markup when adding", P.PRODUCTS_CREATE_SHOW_MARKUP),
                Item("Edit product", P.PRODUCTS_EDIT),
                Item("Delete (archive) product", P.PRODUCTS_DELETE),
                Item("View product history", P.PRODUCT_HISTORY),
                Item("Print product history", P.PRODUCT_HISTORY_PRINT)),
            Group("Adjustments",
                Item("View adjustment list", P.ADJUSTMENTS_INDEX),
                Item("Open an adjustment", P.ADJUSTMENTS_VIEW),
                Item("Add adjustment", P.ADJUSTMENTS_CREATE),
                Item("Edit adjustment", P.ADJUSTMENTS_EDIT),
                Item("Delete adjustment", P.ADJUSTMENTS_DELETE)),
            Group("Customers",
                Item("View customer list", P.CUSTOMER_LIST),
                Item("Open a customer", P.CUSTOMER_VIEW),
                Item("Add customer", P.CUSTOMER_ADD),
                Item("Edit customer", P.CUSTOMER_EDIT),
                Item("Delete customer", P.CUSTOMER_DELETE)),
            Group("Purchase orders",
                Item("View purchase order list", P.PURCHASE_ORDERS_LIST),
                Item("Create purchase order", P.PURCHASE_ORDERS_CREATE),
                Item("Edit purchase order", P.PURCHASE_ORDERS_EDIT),
                Item("Approve purchase order", P.PURCHASE_ORDERS_APPROVE)),
            Group("Shipments",
                Item("View shipment list", P.SHIPMENT_LIST),
                Item("Open a shipment", P.SHIPMENT_VIEW),
                Item("Receive shipment", P.SHIPMENT_CREATE),
                Item("Edit shipment", P.SHIPMENT_EDIT),
                Item("See cost prices", P.SHIPMENT_VIEW_COSTPRICE),
                Item("See sale prices", P.SHIPMENT_VIEW_SALESPRICE)),
            Group("Reports",
                Group("Inventory report", Item("View", P.REPORT_INVENTORY), Item("Print", P.REPORT_INVENTORY_PRINT)),
                Group("Sales report", Item("View", P.REPORT_SALES), Item("Print", P.REPORT_SALES_PRINT)),
                Group("Shipment report", Item("View", P.REPORT_SHIPMENT), Item("Print", P.REPORT_SHIPMENT_PRINT)),
                Group("Refund report", Item("View", P.REPORT_REFUND), Item("Print", P.REPORT_REFUND_PRINT)),
                Group("Adjustments report", Item("View", P.REPORT_ADJUSTMENTS), Item("Print", P.REPORT_ADJUSTMENTS_PRINT))),
            Group("Configuration",
                Item("Product categories", P.CONFIG_PRODUCT_CATEGORIES),
                Item("Category custom fields", P.CONFIG_CATEGORY_FIELDS),
                Item("Locations", P.CONFIG_LOCATIONS),
                Item("Suppliers", P.CONFIG_SUPPLIERS),
                Item("Adjustment reasons", P.CONFIG_ADJUSTMENT_REASONS),
                Item("Refund reasons", P.CONFIG_REFUND_REASONS)),
            Group("Administration",
                Item("Manage users", P.ADMIN_USERS_MANAGEMENT),
                Item("Manage roles", P.ADMIN_ROLES_MANAGEMENT),
                Item("View audit trail", P.ADMIN_AUDIT_TRAIL),
                Group("Database",
                    Item("Data query", P.ADMIN_DATA_QUERY),
                    Item("Database backups", P.CONFIG_DATABASE_BACKUPS),
                    Item("Server logs", P.ADMIN_SERVER_LOGS)))
        };

        // Only a platform admin can grant the database permissions, so nobody else is shown them.
        public static IReadOnlyList<PermissionNode> ForCaller(bool isPlatformAdmin) =>
            isPlatformAdmin ? Roots : Prune(Roots);

        private static IReadOnlyList<PermissionNode> Prune(IEnumerable<PermissionNode> nodes) =>
            nodes.Where(n => n.Permissions.Any(p => !ModuleRegistry.DatabaseModules.Contains(p)))
                .Select(n => n.Permission != null ? n : new PermissionNode(n.Label, null, Prune(n.Children)))
                .ToList();

        private static PermissionNode Item(string label, string permission) =>
            new(label, permission, new PermissionNode[0]);

        private static PermissionNode Group(string label, params PermissionNode[] children) =>
            new(label, null, children);
    }
}
