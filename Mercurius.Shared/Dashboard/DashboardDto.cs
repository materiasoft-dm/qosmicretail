namespace Mercurius.Shared.Dashboard
{
    // Dashboard widget ids — also the WidgetId stored in a user's saved layout.
    public static class DashboardWidgets
    {
        public const string MonthlyTarget = "MonthlyTargetWidget";
        public const string DailySales = "DailySalesWidget";
        public const string PreviousMonthSales = "PreviousMonthSalesWidget";
        public const string MonthlyNewCustomers = "MonthlyNewCustomers";
        public const string ItemsSoldToday = "ItemsSoldTodayWidget";
        public const string StockWarning = "StockWarningWidget";
        public const string ExpiryAlerts = "ExpiryAlertsWidget";
        public const string Shortcuts = "ShortcutWidget";
    }

    // Only the widgets the user may see are filled in (and listed in VisibleWidgets) — each is
    // gated by its own "Dashboard Widget - …" permission, so e.g. revenue never reaches a role
    // that isn't allowed to see it.
    public class DashboardDto
    {
        public List<string> VisibleWidgets { get; set; } = new();

        public decimal TodaySales { get; set; }
        public decimal TodayProfit { get; set; }
        public decimal ThisMonthSales { get; set; }
        public decimal PreviousMonthSales { get; set; }

        public int DailySalesCount { get; set; }
        public int DailySalesTarget { get; set; }

        public int PreviousMonthSalesCount { get; set; }
        public int PreviousMonthSalesTarget { get; set; }

        public int NewCustomersThisMonth { get; set; }

        public int ItemsSoldTodayTotal { get; set; }
        public List<SoldItemDto> ItemsSoldToday { get; set; } = new();

        public int TotalStockItems { get; set; }
        public List<StockItemDto> LowestStockItems { get; set; } = new();

        public int ExpiredCount { get; set; }
        public int ExpiringWithin30Days { get; set; }
        public int ExpiringWithin90Days { get; set; }
        public List<ExpiryItemDto> ExpiringItems { get; set; } = new();
    }

    public class SoldItemDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    public class StockItemDto
    {
        public string ProductCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal CurrentStock { get; set; }
        public decimal LowStockCount { get; set; }
    }

    public class ExpiryItemDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string BatchNumber { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public decimal RemainingQuantity { get; set; }
    }

    // One widget's place in a user's dashboard: its order, whether it spans the full width, and
    // whether they've hidden it.
    public class DashboardWidgetLayoutDto
    {
        public string WidgetId { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool FullWidth { get; set; }
        public bool Hidden { get; set; }
    }
}
