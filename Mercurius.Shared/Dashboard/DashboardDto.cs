namespace Mercurius.Shared.Dashboard
{
    public class DashboardDto
    {
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
}
