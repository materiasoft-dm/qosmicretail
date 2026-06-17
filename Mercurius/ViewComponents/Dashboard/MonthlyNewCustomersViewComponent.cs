using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.ViewComponents.Dashboard
{
    public class MonthlyNewCustomersViewComponent : ViewComponent
    {
        private const int RowLimit = 10;

        private readonly IUnitOfWork _unitOfWork;

        public MonthlyNewCustomersViewComponent(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

            var customersThisMonth = (await _unitOfWork.Repository<Customer>().FindAsync(
                c => c.CreatedDate >= monthStart && c.CreatedDate <= monthEnd)).ToList();

            var model = new MonthlyNewCustomersModel
            {
                NewCustomersThisMonth = customersThisMonth.Count,
                TopCustomers = customersThisMonth.Take(RowLimit).ToList()
            };
            return View(model);
        }
    }

    public class MonthlyNewCustomersModel
    {
        // Stable palette so each avatar gets a deterministic, repeatable colour.
        // The original Blazor widget allocated a fresh Random per render, which is
        // both wasteful and prone to repeat colours on rapid invocations.
        public static readonly string[] AvatarPalette =
        {
            "#1B84FF", "#7239EA", "#17C653", "#F1416C", "#F6C000",
            "#5014D0", "#00B5B5", "#E07F00", "#1FAB89", "#9C27B0"
        };

        public int NewCustomersThisMonth { get; set; }
        public List<Customer> TopCustomers { get; set; } = new();

        public string ColorFor(int index) => AvatarPalette[index % AvatarPalette.Length];
    }
}
