using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Common;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // The dashboard: every widget's figures in one response (scoped to the user's current branch),
    // plus the user's own widget layout.
    [ApiController]
    [Route("api/dashboard")]
    [Authorize]
    public class DashboardApiController : ControllerBase
    {
        private const int DefaultTarget = 10;
        private const int FullWidth = 12;
        private const int HalfWidth = 6;

        // Which permission shows which widget. Expiry alerts had no permission of their own on the
        // classic dashboard (shown to everyone with dashboard access), and still don't.
        private static readonly (string Widget, string? Permission)[] Widgets =
        {
            (DashboardWidgets.Shortcuts, ModuleRegistry.Pages.CLIENT_DASHBOARD_ShortcutWidget),
            (DashboardWidgets.MonthlyTarget, ModuleRegistry.Pages.CLIENT_DASHBOARD_MonthlyTargetWidget),
            (DashboardWidgets.DailySales, ModuleRegistry.Pages.CLIENT_DASHBOARD_DailySalesWidget),
            (DashboardWidgets.PreviousMonthSales, ModuleRegistry.Pages.CLIENT_DASHBOARD_PreviousMonthSalesWidget),
            (DashboardWidgets.MonthlyNewCustomers, ModuleRegistry.Pages.CLIENT_DASHBOARD_MonthlyNewCustomers),
            (DashboardWidgets.ItemsSoldToday, ModuleRegistry.Pages.CLIENT_DASHBOARD_ItemsSoldTodayWidget),
            (DashboardWidgets.StockWarning, ModuleRegistry.Pages.CLIENT_DASHBOARD_StockWarningWidget),
            (DashboardWidgets.ExpiryAlerts, null)
        };

        private readonly IUnitOfWork _unitOfWork;

        public DashboardApiController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // utcOffsetMinutes: the viewer's offset from UTC (e.g. 480 for UTC+8). Sale and customer
        // dates are stored in UTC, so "today" and "this month" are the viewer's calendar day and
        // month turned into UTC ranges — not the server's own clock, which may sit in another zone.
        [HttpGet]
        public async Task<ActionResult<DashboardDto>> Get(int? utcOffsetMinutes = null, CancellationToken ct = default)
        {
            var visible = Widgets
                .Where(w => w.Permission == null || User.HasClaim(MercuriusClaimTypes.AccessPages, w.Permission))
                .Select(w => w.Widget)
                .ToHashSet();
            var dto = new DashboardDto { VisibleWidgets = Widgets.Select(w => w.Widget).Where(visible.Contains).ToList() };

            var locationId = await Mercurius.Services.CurrentLocation.GetIdAsync(_unitOfWork, User);
            var offset = TimeSpan.FromMinutes(Math.Clamp(
                utcOffsetMinutes ?? (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes, -14 * 60, 14 * 60));
            var localToday = (DateTime.UtcNow + offset).Date;
            DateTime Utc(DateTime localMidnight) => DateTime.SpecifyKind(localMidnight - offset, DateTimeKind.Utc);
            var today = Utc(localToday);
            var dayEnd = Utc(localToday.AddDays(1)).AddTicks(-1);
            var firstOfThisMonthLocal = new DateTime(localToday.Year, localToday.Month, 1);
            var firstOfThisMonth = Utc(firstOfThisMonthLocal);
            var lastTickThisMonth = Utc(firstOfThisMonthLocal.AddMonths(1)).AddTicks(-1);
            var firstOfLastMonth = Utc(firstOfThisMonthLocal.AddMonths(-1));
            var lastTickLastMonth = firstOfThisMonth.AddTicks(-1);

            if (visible.Contains(DashboardWidgets.DailySales))
            {
                dto.DailySalesTarget = await GetOrCreateTargetAsync(locationId, LocationSettingKeys.DailySalesTarget, ct);
                dto.DailySalesCount = await _unitOfWork.Repository<Invoice>().CountAsync(
                    i => i.StatusId != (int)StatusCollection.InvoiceStatus.Deleted
                      && i.InvoiceDate >= today && i.InvoiceDate <= dayEnd && i.LocationId == locationId, ct);
            }

            if (visible.Contains(DashboardWidgets.PreviousMonthSales))
            {
                // The branch's own monthly target (set on the Locations page) wins over the setting.
                var location = await _unitOfWork.Repository<Location>().GetByIdAsync(locationId, ct);
                dto.PreviousMonthSalesTarget = location?.MetaMonthlyTargetSalesCount is > 0
                    ? (int)Math.Round(location.MetaMonthlyTargetSalesCount.Value)
                    : await GetOrCreateTargetAsync(locationId, LocationSettingKeys.MonthlySalesTarget, ct);
                dto.PreviousMonthSalesCount = await _unitOfWork.Repository<Invoice>().CountAsync(
                    i => i.StatusId != (int)StatusCollection.InvoiceStatus.Deleted
                      && i.InvoiceDate >= firstOfLastMonth && i.InvoiceDate <= lastTickLastMonth && i.LocationId == locationId, ct);
            }

            if (visible.Contains(DashboardWidgets.MonthlyTarget))
            {
                // Today / this month / last month revenue, and today's profit.
                var invoices = _unitOfWork.Query<Invoice>()
                    .Where(i => i.StatusId != (int)StatusCollection.InvoiceStatus.Deleted
                             && i.LocationId == locationId
                             && i.InvoiceDate >= firstOfLastMonth && i.InvoiceDate <= lastTickThisMonth)
                    .ToList();
                var invoiceIds = invoices.Select(i => i.Id).ToHashSet();
                var items = invoiceIds.Count == 0 ? new List<InvoiceItem>() : _unitOfWork.Query<InvoiceItem>().Where(ii => invoiceIds.Contains(ii.InvoiceId)).ToList();
                var itemsByInvoice = items.GroupBy(ii => ii.InvoiceId).ToDictionary(g => g.Key, g => g.ToList());

                decimal SumLineTotal(IEnumerable<InvoiceItem> lines) =>
                    lines.Sum(x => x.CustomTotalPrice.HasValue && x.CustomTotalPrice.Value > 0 ? x.CustomTotalPrice.Value : x.SalePrice * x.Quantity);
                decimal SumCost(IEnumerable<InvoiceItem> lines) => lines.Sum(x => x.CostPrice * x.Quantity);
                IEnumerable<InvoiceItem> ItemsBetween(DateTime start, DateTime end) =>
                    invoices.Where(i => i.InvoiceDate >= start && i.InvoiceDate <= end)
                        .SelectMany(i => itemsByInvoice.TryGetValue(i.Id, out var l) ? l : Enumerable.Empty<InvoiceItem>());

                var todayItems = ItemsBetween(today, dayEnd).ToList();
                dto.TodaySales = SumLineTotal(todayItems);
                dto.TodayProfit = dto.TodaySales - SumCost(todayItems);
                dto.ThisMonthSales = SumLineTotal(ItemsBetween(firstOfThisMonth, lastTickThisMonth));
                dto.PreviousMonthSales = SumLineTotal(ItemsBetween(firstOfLastMonth, lastTickLastMonth));
            }

            Dictionary<int, Product>? products = null;
            async Task<Dictionary<int, Product>> ProductsAsync() =>
                products ??= (await _unitOfWork.Repository<Product>().GetAllAsync(ct)).ToDictionary(p => p.Id);

            if (visible.Contains(DashboardWidgets.ItemsSoldToday))
            {
                var todaysInvoiceIds = (await _unitOfWork.Repository<Invoice>().FindAsync(
                    i => i.StatusId != (int)StatusCollection.InvoiceStatus.Deleted && i.InvoiceDate >= today && i.InvoiceDate <= dayEnd
                      && i.LocationId == locationId, ct))
                    .Select(i => i.Id).ToHashSet();
                var soldItems = todaysInvoiceIds.Count == 0 ? new List<InvoiceItem>()
                    : (await _unitOfWork.Repository<InvoiceItem>().FindAsync(ii => todaysInvoiceIds.Contains(ii.InvoiceId), ct)).ToList();
                var byId = await ProductsAsync();
                dto.ItemsSoldToday = soldItems
                    .GroupBy(ii => ii.ProductId)
                    .Select(g => new SoldItemDto
                    {
                        ProductName = byId.TryGetValue(g.Key, out var p) ? p.Name ?? string.Empty : string.Empty,
                        ProductCode = byId.TryGetValue(g.Key, out var p2) ? p2.ProductCode ?? string.Empty : string.Empty,
                        Quantity = g.Sum(ii => ii.Quantity)
                    })
                    .OrderByDescending(s => s.Quantity)
                    .ToList();
                dto.ItemsSoldTodayTotal = (int)soldItems.Sum(i => i.Quantity);
            }

            if (visible.Contains(DashboardWidgets.MonthlyNewCustomers))
            {
                dto.NewCustomersThisMonth = await _unitOfWork.Repository<Customer>().CountAsync(
                    c => c.CreatedDate >= firstOfThisMonth && c.CreatedDate <= lastTickThisMonth, ct);
            }

            if (visible.Contains(DashboardWidgets.StockWarning))
            {
                var activeProducts = (await _unitOfWork.Repository<Product>().FindAsync(p => p.IsActive, ct)).ToList();
                dto.TotalStockItems = activeProducts.Count;
                dto.LowestStockItems = activeProducts.OrderBy(p => p.CurrentStock).Take(10)
                    .Select(p => new StockItemDto { ProductCode = p.ProductCode ?? string.Empty, Name = p.Name ?? string.Empty, CurrentStock = p.CurrentStock, LowStockCount = p.LowStockCount })
                    .ToList();
            }

            if (visible.Contains(DashboardWidgets.ExpiryAlerts))
            {
                var now = DateTime.UtcNow;
                var ninetyDays = now.AddDays(90);
                var batches = (await _unitOfWork.Repository<MedicineBatch>().FindAsync(b => b.IsActive && b.ExpiryDate <= ninetyDays, ct)).ToList();
                var byId = await ProductsAsync();
                dto.ExpiredCount = batches.Count(b => b.ExpiryDate < now);
                dto.ExpiringWithin30Days = batches.Count(b => b.ExpiryDate >= now && b.ExpiryDate <= now.AddDays(30));
                dto.ExpiringWithin90Days = batches.Count(b => b.ExpiryDate > now.AddDays(30) && b.ExpiryDate <= ninetyDays);
                dto.ExpiringItems = batches.OrderBy(b => b.ExpiryDate).Take(10)
                    .Select(b => new ExpiryItemDto
                    {
                        ProductName = byId.TryGetValue(b.ProductId, out var p) ? p.Name ?? string.Empty : "Unknown",
                        BatchNumber = b.BatchNumber,
                        ExpiryDate = b.ExpiryDate,
                        RemainingQuantity = b.RemainingQuantity
                    }).ToList();
            }

            return Ok(dto);
        }

        // The user's own arrangement of their dashboard. Stored in UserDashboardLayout (the classic
        // dashboard's grid table): Y = order, Width = 12 (full) or 6 (half), IsActive = shown.
        [HttpGet("layout")]
        public async Task<ActionResult<List<DashboardWidgetLayoutDto>>> GetLayout(CancellationToken ct = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var saved = await _unitOfWork.Repository<UserDashboardLayout>().FindAsync(l => l.UserId == userId, ct);
            var ordered = saved.OrderBy(l => l.Y).ThenBy(l => l.X).ToList();
            return Ok(ordered.Select((l, index) => new DashboardWidgetLayoutDto
            {
                WidgetId = l.WidgetId,
                Order = index,
                FullWidth = l.Width >= FullWidth,
                Hidden = !l.IsActive
            }).ToList());
        }

        [HttpPut("layout")]
        public async Task<IActionResult> SaveLayout(List<DashboardWidgetLayoutDto> layout, CancellationToken ct = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var known = Widgets.Select(w => w.Widget).ToHashSet();
            var repo = _unitOfWork.Repository<UserDashboardLayout>();
            foreach (var existing in await repo.FindAsync(l => l.UserId == userId, ct))
            {
                await repo.DeleteAsync(existing.Id, ct);
            }
            foreach (var item in layout.Where(l => known.Contains(l.WidgetId)).OrderBy(l => l.Order))
            {
                await repo.AddAsync(new UserDashboardLayout
                {
                    UserId = userId,
                    WidgetId = item.WidgetId,
                    X = 0,
                    Y = item.Order,
                    Width = item.FullWidth ? FullWidth : HalfWidth,
                    Height = 1,
                    IsActive = !item.Hidden
                }, ct);
            }
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok();
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
