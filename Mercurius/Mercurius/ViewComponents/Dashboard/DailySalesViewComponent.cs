using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.ViewComponents.Dashboard
{
    public class DailySalesViewComponent : ViewComponent
    {
        private const int DefaultTarget = 10;

        private readonly IUnitOfWork _unitOfWork;

        public DailySalesViewComponent(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var locationId = await DashboardLocationContext.GetCurrentLocationIdAsync(_unitOfWork, UserClaimsPrincipal);

            var settings = await _unitOfWork.Repository<LocationSetting>().FindAsync(
                s => s.SettingCode == LocationSettingKeys.DailySalesTarget && s.LocationId == locationId);
            var target = settings.FirstOrDefault();

            if (target == null)
            {
                target = new LocationSetting
                {
                    LocationId = locationId,
                    SettingCode = LocationSettingKeys.DailySalesTarget,
                    SettingValue = DefaultTarget.ToString()
                };
                await _unitOfWork.Repository<LocationSetting>().AddAsync(target);
                await _unitOfWork.SaveChangesAsync();
            }

            if (!int.TryParse(target.SettingValue, out var quota) || quota <= 0)
            {
                quota = DefaultTarget;
            }

            var dayStart = DateTime.Today;
            var dayEnd = dayStart.AddDays(1).AddTicks(-1);
            var totalSales = await _unitOfWork.Repository<Invoice>().CountAsync(
                i => i.StatusId == (int)StatusCollection.InvoiceStatus.Finalized
                  && i.InvoiceDate >= dayStart
                  && i.InvoiceDate <= dayEnd
                  && i.LocationId == locationId);

            var model = new SalesAgainstQuotaModel
            {
                Title = "Daily Sales",
                Subtitle = "Sales finalized today vs the daily target",
                TotalSales = totalSales,
                Quota = quota
            };
            return View("SalesAgainstQuota", model);
        }
    }

    public class SalesAgainstQuotaModel
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public int TotalSales { get; set; }
        public int Quota { get; set; }

        public decimal Percentage =>
            Quota <= 0
                ? 0
                : Math.Round(((decimal)TotalSales / Quota) * 100m, 2);
    }
}
