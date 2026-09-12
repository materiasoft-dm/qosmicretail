using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // Consolidates all 7 dashboard ViewComponents (DailySales, MonthlyTarget,
    // PreviousMonthSales, ItemsSoldToday, StockWarning, ExpiryAlerts, MonthlyNewCustomers) into
    // one JSON response for the Blazor dashboard — same underlying queries as each MVC
    // ViewComponent, just returned together instead of rendered as separate partial views.
    [ApiController]
    [Route("api/dashboard")]
    [Authorize]
    public class DashboardApiController : ControllerBase
    {
        private const int DefaultTarget = 10;
        private readonly IUnitOfWork _unitOfWork;

        public DashboardApiController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct = default)
        {
            var locationId = await Mercurius.ViewComponents.Dashboard.DashboardLocationContext.GetCurrentLocationIdAsync(_unitOfWork, User);
            var today = DateTime.Today;
            var dayEnd = today.AddDays(1).AddTicks(-1);
            var firstOfThisMonth = new DateTime(today.Year, today.Month, 1);
            var firstOfNextMonth = firstOfThisMonth.AddMonths(1);
            var lastTickThisMonth = firstOfNextMonth.AddTicks(-1);
            var firstOfLastMonth = firstOfThisMonth.AddMonths(-1);
            var lastTickLastMonth = firstOfThisMonth.AddTicks(-1);

            var dto = new DashboardDto();

            // --- Daily Sales / Previous Month Sales targets (LocationSetting, get-or-create) ---
            dto.DailySalesTarget = await GetOrCreateTargetAsync(locationId, LocationSettingKeys.DailySalesTarget, ct);
            dto.PreviousMonthSalesTarget = await GetOrCreateTargetAsync(locationId, LocationSettingKeys.MonthlySalesTarget, ct);

            dto.DailySalesCount = await _unitOfWork.Repository<Invoice>().CountAsync(
                i => i.StatusId != (int)StatusCollection.InvoiceStatus.Deleted
                  && i.InvoiceDate >= today && i.InvoiceDate <= dayEnd && i.LocationId == locationId, ct);

            dto.PreviousMonthSalesCount = await _unitOfWork.Repository<Invoice>().CountAsync(
                i => i.StatusId != (int)StatusCollection.InvoiceStatus.Deleted
                  && i.InvoiceDate >= firstOfLastMonth && i.InvoiceDate <= lastTickLastMonth && i.LocationId == locationId, ct);

            // --- Monthly Target (today/this month/last month revenue) ---
            var allInvoices = _unitOfWork.Query<Invoice>()
                .Where(i => i.StatusId != (int)StatusCollection.InvoiceStatus.Deleted
                         && i.LocationId == locationId
                         && i.InvoiceDate >= firstOfLastMonth && i.InvoiceDate <= lastTickThisMonth)
                .ToList();
            var invoiceIds = allInvoices.Select(i => i.Id).ToHashSet();
            var allItems = invoiceIds.Count == 0 ? new() : _unitOfWork.Query<InvoiceItem>().Where(ii => invoiceIds.Contains(ii.InvoiceId)).ToList();
            var itemsByInvoice = allItems.GroupBy(ii => ii.InvoiceId).ToDictionary(g => g.Key, g => g.ToList());

            decimal SumLineTotal(System.Collections.Generic.IEnumerable<InvoiceItem> items) =>
                items.Sum(x => x.CustomTotalPrice.HasValue && x.CustomTotalPrice.Value > 0 ? x.CustomTotalPrice.Value : x.SalePrice * x.Quantity);
            decimal SumCost(System.Collections.Generic.IEnumerable<InvoiceItem> items) => items.Sum(x => x.CostPrice * x.Quantity);
            System.Collections.Generic.IEnumerable<InvoiceItem> ItemsForRange(DateTime start, DateTime end) =>
                allInvoices.Where(i => i.InvoiceDate >= start && i.InvoiceDate <= end).SelectMany(i => itemsByInvoice.TryGetValue(i.Id, out var l) ? l : Enumerable.Empty<InvoiceItem>());

            var todayItems = ItemsForRange(today, dayEnd).ToList();
            dto.TodaySales = SumLineTotal(todayItems);
            dto.TodayProfit = dto.TodaySales - SumCost(todayItems);
            dto.ThisMonthSales = SumLineTotal(ItemsForRange(firstOfThisMonth, lastTickThisMonth));
            dto.PreviousMonthSales = SumLineTotal(ItemsForRange(firstOfLastMonth, lastTickLastMonth));

            // --- Items sold today ---
            var todaysInvoiceIds = (await _unitOfWork.Repository<Invoice>().FindAsync(
                i => i.StatusId != (int)StatusCollection.InvoiceStatus.Deleted && i.InvoiceDate >= today && i.InvoiceDate <= dayEnd, ct))
                .Select(i => i.Id).ToHashSet();
            var soldItems = todaysInvoiceIds.Count == 0 ? new() : (await _unitOfWork.Repository<InvoiceItem>().FindAsync(ii => todaysInvoiceIds.Contains(ii.InvoiceId), ct)).ToList();
            var products = (await _unitOfWork.Repository<Product>().GetAllAsync(ct)).ToDictionary(p => p.Id);
            dto.ItemsSoldToday = soldItems.Select(ii => new SoldItemDto
            {
                ProductName = products.TryGetValue(ii.ProductId, out var p) ? p.Name ?? string.Empty : string.Empty,
                ProductCode = products.TryGetValue(ii.ProductId, out var p2) ? p2.ProductCode ?? string.Empty : string.Empty,
                Quantity = ii.Quantity
            }).ToList();
            dto.ItemsSoldTodayTotal = (int)soldItems.Sum(i => i.Quantity);

            // --- New customers this month ---
            dto.NewCustomersThisMonth = await _unitOfWork.Repository<Customer>().CountAsync(
                c => c.CreatedDate >= firstOfThisMonth && c.CreatedDate <= lastTickThisMonth, ct);

            // --- Stock warning (lowest stock, active products) ---
            var activeProducts = (await _unitOfWork.Repository<Product>().FindAsync(p => p.IsActive, ct)).ToList();
            dto.TotalStockItems = activeProducts.Count;
            dto.LowestStockItems = activeProducts.OrderBy(p => p.CurrentStock).Take(10)
                .Select(p => new StockItemDto { ProductCode = p.ProductCode ?? string.Empty, Name = p.Name ?? string.Empty, CurrentStock = p.CurrentStock, LowStockCount = p.LowStockCount })
                .ToList();

            // --- Expiry alerts ---
            var now = DateTime.UtcNow;
            var ninetyDays = now.AddDays(90);
            var batches = (await _unitOfWork.Repository<MedicineBatch>().FindAsync(b => b.IsActive && b.ExpiryDate <= ninetyDays, ct)).ToList();
            dto.ExpiredCount = batches.Count(b => b.ExpiryDate < now);
            dto.ExpiringWithin30Days = batches.Count(b => b.ExpiryDate >= now && b.ExpiryDate <= now.AddDays(30));
            dto.ExpiringWithin90Days = batches.Count(b => b.ExpiryDate > now.AddDays(30) && b.ExpiryDate <= ninetyDays);
            dto.ExpiringItems = batches.OrderBy(b => b.ExpiryDate).Take(10)
                .Select(b => new ExpiryItemDto
                {
                    ProductName = products.TryGetValue(b.ProductId, out var p) ? p.Name ?? string.Empty : "Unknown",
                    BatchNumber = b.BatchNumber,
                    ExpiryDate = b.ExpiryDate,
                    RemainingQuantity = b.RemainingQuantity
                }).ToList();

            return Ok(dto);
        }

        private async Task<int> GetOrCreateTargetAsync(int locationId, string settingCode, CancellationToken ct)
        {
            var settings = await _unitOfWork.Repository<LocationSetting>().FindAsync(s => s.SettingCode == settingCode && s.LocationId == locationId, ct);
            var target = settings.FirstOrDefault();
            if (target == null)
            {
                target = new LocationSetting { LocationId = locationId, SettingCode = settingCode, SettingValue = DefaultTarget.ToString() };
                await _unitOfWork.Repository<LocationSetting>().AddAsync(target, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            return int.TryParse(target.SettingValue, out var v) && v > 0 ? v : DefaultTarget;
        }
    }
}
