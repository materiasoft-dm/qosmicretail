using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.ViewComponents.Dashboard
{
    public class PreviousMonthSalesViewComponent : ViewComponent
    {
        private const int DefaultTarget = 10;

        private readonly IUnitOfWork _unitOfWork;

        public PreviousMonthSalesViewComponent(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var locationId = await DashboardLocationContext.GetCurrentLocationIdAsync(_unitOfWork, UserClaimsPrincipal);

            var settings = await _unitOfWork.Repository<LocationSetting>().FindAsync(
                s => s.SettingCode == LocationSettingKeys.MonthlySalesTarget && s.LocationId == locationId);
            var target = settings.FirstOrDefault();

            if (!int.TryParse(target?.SettingValue, out var quota) || quota <= 0)
            {
                quota = DefaultTarget;
            }

            var (start, end) = PreviousMonthRange(DateTime.Today);
            var totalSales = await _unitOfWork.Repository<Invoice>().CountAsync(
                i => i.StatusId == (int)StatusCollection.InvoiceStatus.Finalized
                  && i.InvoiceDate >= start
                  && i.InvoiceDate <= end
                  && i.LocationId == locationId);

            var model = new SalesAgainstQuotaModel
            {
                Title = "Previous Month Sales",
                Subtitle = $"{start:MMM yyyy} finalized invoices vs the monthly target",
                TotalSales = totalSales,
                Quota = quota
            };
            return View("SalesAgainstQuota", model);
        }

        private static (DateTime start, DateTime end) PreviousMonthRange(DateTime today)
        {
            var firstOfThisMonth = new DateTime(today.Year, today.Month, 1);
            var firstOfLastMonth = firstOfThisMonth.AddMonths(-1);
            var endOfLastMonth = firstOfThisMonth.AddTicks(-1);
            return (firstOfLastMonth, endOfLastMonth);
        }
    }
}
