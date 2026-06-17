using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mercurius.Common
{
    public class ModuleRegistry
    {
        public static IReadOnlyList<string> Modules { get; } = new[]
        {
            ModuleRegistry.Pages.HOME_PAGE,
                    ModuleRegistry.Pages.PRODUCTS_INDEX,
                    ModuleRegistry.Pages.PRODUCTS_CREATE,
                    ModuleRegistry.Pages.PRODUCTS_EDIT,
                    ModuleRegistry.Pages.PRODUCTS_DELETE,
                    ModuleRegistry.Pages.PRODUCTS_CREATE_SHOW_COST,

                    ModuleRegistry.Pages.PRODUCTS_CREATE_SHOW_MARKUP,
                    ModuleRegistry.Pages.PRODUCTS_INDEX_SHOW_COST,
                    ModuleRegistry.Pages.PRODUCT_HISTORY,
                    ModuleRegistry.Pages.PRODUCT_HISTORY_PRINT,
                    ModuleRegistry.Pages.SHIPMENT_CREATE,


                    ModuleRegistry.Pages.SHIPMENT_LIST,
                    ModuleRegistry.Pages.SHIPMENT_EDIT,
                    ModuleRegistry.Pages.SHIPMENT_VIEW,
                    ModuleRegistry.Pages.SHIPMENT_VIEW_COSTPRICE,
                    ModuleRegistry.Pages.SHIPMENT_VIEW_SALESPRICE,
                    ModuleRegistry.Pages.NEWSALE_CREATE,
                    ModuleRegistry.Pages.NEWSALE_VIEW_COSTPRICE,
                    ModuleRegistry.Pages.INVOICE_INDEX,
                    ModuleRegistry.Pages.INVOICE_VIEW,
                    ModuleRegistry.Pages.INVOICE_VIEW_SHOWCOST,
                    ModuleRegistry.Pages.INVOICE_UPDATE_PAYMENT,

                    ModuleRegistry.Pages.ADJUSTMENTS_CREATE,
                    ModuleRegistry.Pages.ADJUSTMENTS_DELETE,
                    ModuleRegistry.Pages.ADJUSTMENTS_VIEW,
                    ModuleRegistry.Pages.ADJUSTMENTS_EDIT,
                    ModuleRegistry.Pages.ADJUSTMENTS_INDEX,
                    ModuleRegistry.Pages.CLIENT_DASHBOARD,
                    ModuleRegistry.Pages.CUSTOMER_ADD,
                    ModuleRegistry.Pages.CUSTOMER_DELETE,
                    ModuleRegistry.Pages.CUSTOMER_EDIT,
                    ModuleRegistry.Pages.CUSTOMER_LIST,
                    ModuleRegistry.Pages.CUSTOMER_VIEW,
                    ModuleRegistry.Pages.REPORT_INVENTORY,
                    ModuleRegistry.Pages.REPORT_INVENTORY_PRINT,

                    ModuleRegistry.Pages.REPORT_SALES,
                    ModuleRegistry.Pages.REPORT_SALES_PRINT,
                                        ModuleRegistry.Pages.REPORT_SHIPMENT,
                    ModuleRegistry.Pages.REPORT_SHIPMENT_PRINT,
                    ModuleRegistry.Pages.REPORT_REFUND,
                    ModuleRegistry.Pages.REPORT_REFUND_PRINT,
                                        ModuleRegistry.Pages.REPORT_ADJUSTMENTS,
                    ModuleRegistry.Pages.REPORT_ADJUSTMENTS_PRINT,

                    ModuleRegistry.Pages.CLIENT_DASHBOARD_MonthlySalesWidget,
                    ModuleRegistry.Pages.CLIENT_DASHBOARD_MonthlyNewCustomers,
                    ModuleRegistry.Pages.CLIENT_DASHBOARD_MonthlyTargetWidget,
                    ModuleRegistry.Pages.CLIENT_DASHBOARD_ShortcutWidget,
                    ModuleRegistry.Pages.CLIENT_DASHBOARD_StockWarningWidget,
                    ModuleRegistry.Pages.CLIENT_DASHBOARD_DailySalesWidget,
                    ModuleRegistry.Pages.CLIENT_DASHBOARD_PreviousMonthSalesWidget,
                    ModuleRegistry.Pages.CLIENT_DASHBOARD_ItemsSoldTodayWidget,
                    ModuleRegistry.Pages.CONFIG_PRODUCT_CATEGORIES,
                    ModuleRegistry.Pages.CONFIG_LOCATIONS,
                    ModuleRegistry.Pages.CONFIG_COLORS,
                    ModuleRegistry.Pages.CONFIG_SIZES,
                    ModuleRegistry.Pages.CONFIG_SUPPLIERS,
                    ModuleRegistry.Pages.CONFIG_ADJUSTMENT_REASONS,
                    ModuleRegistry.Pages.CONFIG_CATEGORY_FIELDS,
                    ModuleRegistry.Pages.DOCTORS_LIST,
                    ModuleRegistry.Pages.DOCTORS_CREATE,
                    ModuleRegistry.Pages.DOCTORS_EDIT,
                    ModuleRegistry.Pages.DOCTORS_DELETE,
                    ModuleRegistry.Pages.PRESCRIPTIONS_LIST,
                    ModuleRegistry.Pages.PRESCRIPTIONS_CREATE,
                    ModuleRegistry.Pages.PRESCRIPTIONS_EDIT,
                    ModuleRegistry.Pages.PRESCRIPTIONS_DELETE,
                    ModuleRegistry.Pages.PURCHASE_ORDERS_LIST,
                    ModuleRegistry.Pages.PURCHASE_ORDERS_CREATE,
                    ModuleRegistry.Pages.PURCHASE_ORDERS_EDIT,
                    ModuleRegistry.Pages.PURCHASE_ORDERS_APPROVE,
                    ModuleRegistry.Pages.ADMIN_ROLES_MANAGEMENT,
                    ModuleRegistry.Pages.ADMIN_USERS_MANAGEMENT,
                    ModuleRegistry.Pages.WHOLESALE_INDEX,
                    ModuleRegistry.Pages.WHOLESALE_DELETE,
            ModuleRegistry.Pages.WHOLESALE_VIEW
        };

        public static class Pages
        {
            public const string HOME_PAGE = "Dashboard Page";
            public const string PRODUCTS_INDEX = "Product List Page";
            public const string PRODUCTS_INDEX_SHOW_COST = "Product List Page - Show Cost Price";
            public const string PRODUCTS_CREATE = "Product Create Page";
            public const string PRODUCTS_CREATE_SHOW_COST = "Product Create Page - Show Cost Price";

            public const string PRODUCTS_CREATE_SHOW_MARKUP = "Product Create Page - Show Markup";
            public const string PRODUCTS_EDIT = "Product Edit Page";
            public const string PRODUCTS_DELETE = "Product Delete";
            public const string PRODUCT_HISTORY = "Product History";
            public const string PRODUCT_HISTORY_PRINT = "Product History - Print";

            public const string SHIPMENT_CREATE = "Shipment Add New";
            public const string SHIPMENT_LIST = "Shipment List";
            public const string SHIPMENT_VIEW = "Shipment View";
            public const string SHIPMENT_EDIT = "Shipment Edit";
            public const string SHIPMENT_VIEW_COSTPRICE = "Shipment - View Cost Price Columns";
            public const string SHIPMENT_VIEW_SALESPRICE = "Shipment - View Sales Price Columns";
            public const string NEWSALE_CREATE = "Create New Sale";
            public const string NEWSALE_VIEW_COSTPRICE = "New Sale - View Cost Price Columns";
            public const string INVOICE_INDEX = "Invoice List";
            public const string INVOICE_VIEW = "Invoice View";
            public const string INVOICE_VIEW_SHOWCOST = "Invoice View - Show Cost";
            public const string INVOICE_UPDATE_PAYMENT = "Invoice View - Update Payment";
            public const string ADJUSTMENTS_CREATE = "Adjustments Create Page";
            public const string ADJUSTMENTS_INDEX = "Adjustments Index Page";
            public const string ADJUSTMENTS_DELETE = "Adjustments Delete";
            public const string ADJUSTMENTS_VIEW = "Adjustments View Page";
            public const string ADJUSTMENTS_EDIT = "Adjustments Edit Page";
            public const string CLIENT_DASHBOARD = "Client App Dashboard";

            public const string CUSTOMER_LIST = "Customer List";
            public const string CUSTOMER_ADD = "Customer Add";
            public const string CUSTOMER_EDIT = "Customer Edit";
            public const string CUSTOMER_DELETE = "Customer Delete";
            public const string CUSTOMER_VIEW = "Customer View";

            public const string REPORT_INVENTORY = "Report - Inventory";
            public const string REPORT_INVENTORY_PRINT = "Report - Inventory - Print";

            public const string REPORT_SALES = "Report - Sales";
            public const string REPORT_SALES_PRINT = "Report - Sales - Print";
            public const string REPORT_SHIPMENT = "Report - Shipment";
            public const string REPORT_SHIPMENT_PRINT = "Report - Shipment - Print";


            public const string REPORT_REFUND = "Report - Refund";
            public const string REPORT_REFUND_PRINT = "Report - Refund - Print";

            public const string REPORT_ADJUSTMENTS = "Report - Adjustments";
            public const string REPORT_ADJUSTMENTS_PRINT = "Report - Adjustments - Print";

            public const string CLIENT_DASHBOARD_MonthlySalesWidget = "Dashboard Widget - Monthly Sales";
            public const string CLIENT_DASHBOARD_MonthlyNewCustomers = "Dashboard Widget - Monthly New Customers";
            public const string CLIENT_DASHBOARD_MonthlyTargetWidget = "Dashboard Widget - Monthly Target";

            public const string CLIENT_DASHBOARD_ShortcutWidget = "Dashboard Widget -Shortcut Links";
            public const string CLIENT_DASHBOARD_StockWarningWidget = "Dashboard Widget - Stock Warning";
            public const string CLIENT_DASHBOARD_DailySalesWidget = "Dashboard Widget - Daily Sales";
            public const string CLIENT_DASHBOARD_PreviousMonthSalesWidget = "Dashboard Widget - Previous Month Sales";
            public const string CLIENT_DASHBOARD_ItemsSoldTodayWidget = "Dashboard Widget - Items Sold Today";
            public const string CONFIG_PRODUCT_CATEGORIES = "Configuration - Product Categories";
            public const string CONFIG_LOCATIONS = "Configuration - Locations";
            public const string CONFIG_COLORS = "Configuration - Colors";
            public const string CONFIG_SIZES = "Configuration - Sizes";
            public const string CONFIG_SUPPLIERS = "Configuration - Suppliers";
            public const string CONFIG_ADJUSTMENT_REASONS = "Configuration - Adjustment Reasons";
            public const string CONFIG_CATEGORY_FIELDS = "Configuration - Category Custom Fields";
            public const string DOCTORS_LIST = "Doctor List Page";
            public const string DOCTORS_CREATE = "Doctor Create Page";
            public const string DOCTORS_EDIT = "Doctor Edit Page";
            public const string DOCTORS_DELETE = "Doctor Delete";
            public const string PRESCRIPTIONS_LIST = "Prescription List Page";
            public const string PRESCRIPTIONS_CREATE = "Prescription Create Page";
            public const string PRESCRIPTIONS_EDIT = "Prescription Edit Page";
            public const string PRESCRIPTIONS_DELETE = "Prescription Delete";
            public const string PURCHASE_ORDERS_LIST = "Purchase Order List";
            public const string PURCHASE_ORDERS_CREATE = "Purchase Order Create";
            public const string PURCHASE_ORDERS_EDIT = "Purchase Order Edit";
            public const string PURCHASE_ORDERS_APPROVE = "Purchase Order Approve";
            public const string ADMIN_USERS_MANAGEMENT = "ADMIN - User management";
            public const string ADMIN_ROLES_MANAGEMENT = "ADMIN - Roles management";

            public const string WHOLESALE_INDEX = "Whole Sale List Page";
            public const string WHOLESALE_DELETE = "Whole Sale Delete";
            public const string WHOLESALE_VIEW = "Whole Sale View";
        }
    }
}
