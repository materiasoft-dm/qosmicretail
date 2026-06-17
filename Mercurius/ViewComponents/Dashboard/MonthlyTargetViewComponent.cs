using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.ViewComponents.Dashboard
{
    public class MonthlyTargetViewComponent : ViewComponent
    {
        private readonly IUnitOfWork _unitOfWork;

        public MonthlyTargetViewComponent(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var locationId = await DashboardLocationContext.GetCurrentLocationIdAsync(_unitOfWork, UserClaimsPrincipal);

            var today = DateTime.Today;
            var dayEnd = today.AddDays(1).AddTicks(-1);
            var firstOfThisMonth = new DateTime(today.Year, today.Month, 1);
            var firstOfNextMonth = firstOfThisMonth.AddMonths(1);
            var lastTickThisMonth = firstOfNextMonth.AddTicks(-1);
            var firstOfLastMonth = firstOfThisMonth.AddMonths(-1);
            var lastTickLastMonth = firstOfThisMonth.AddTicks(-1);

            // Pull every finalized invoice we care about in one pass, then split client-side.
            // Spans 'last month start' to 'today end' so all three figures come from the same query.
            var allInvoices = (await _unitOfWork.Repository<Invoice>().FindAsync(
                i => i.StatusId == (int)StatusCollection.InvoiceStatus.Finalized
                  && i.LocationId == locationId
                  && i.InvoiceDate >= firstOfLastMonth
                  && i.InvoiceDate <= lastTickThisMonth)).ToList();

            var invoiceIds = allInvoices.Select(i => i.Id).ToHashSet();
            var allItems = invoiceIds.Count == 0
                ? new List<InvoiceItem>()
                : (await _unitOfWork.Repository<InvoiceItem>().FindAsync(ii => invoiceIds.Contains(ii.InvoiceId))).ToList();
            var itemsByInvoice = allItems.GroupBy(ii => ii.InvoiceId).ToDictionary(g => g.Key, g => g.ToList());

            decimal SumLineTotal(IEnumerable<InvoiceItem> items) =>
                items.Sum(x => x.CustomTotalPrice.HasValue && x.CustomTotalPrice.Value > 0
                    ? x.CustomTotalPrice.Value
                    : x.SalePrice * x.Quantity);

            decimal SumCost(IEnumerable<InvoiceItem> items) =>
                items.Sum(x => x.CostPrice * x.Quantity);

            IEnumerable<InvoiceItem> ItemsForRange(DateTime rangeStart, DateTime rangeEnd) =>
                allInvoices
                    .Where(i => i.InvoiceDate >= rangeStart && i.InvoiceDate <= rangeEnd)
                    .SelectMany(i => itemsByInvoice.TryGetValue(i.Id, out var list) ? list : Enumerable.Empty<InvoiceItem>());

            var todayItems = ItemsForRange(today, dayEnd).ToList();
            var thisMonthItems = ItemsForRange(firstOfThisMonth, lastTickThisMonth);
            var lastMonthItems = ItemsForRange(firstOfLastMonth, lastTickLastMonth);

            var todaySales = SumLineTotal(todayItems);
            var todayCost = SumCost(todayItems);

            var model = new MonthlyTargetModel
            {
                TotalRevenueToday = todaySales - todayCost,
                TotalSales = todaySales,
                TotalSalesThisMonth = SumLineTotal(thisMonthItems),
                TotalSalesPreviousMonth = SumLineTotal(lastMonthItems)
            };
            return View(model);
        }
    }

    public class MonthlyTargetModel
    {
        public decimal TotalRevenueToday { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalSalesThisMonth { get; set; }
        public decimal TotalSalesPreviousMonth { get; set; }
    }
}
