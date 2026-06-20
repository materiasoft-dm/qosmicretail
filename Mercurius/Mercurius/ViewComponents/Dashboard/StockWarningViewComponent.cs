using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.ViewComponents.Dashboard
{
    public class StockWarningViewComponent : ViewComponent
    {
        private const int RowLimit = 10;

        private readonly IUnitOfWork _unitOfWork;

        public StockWarningViewComponent(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var activeProducts = await _unitOfWork.Repository<Product>().FindAsync(p => p.IsActive);
            var ordered = activeProducts
                .OrderBy(p => p.CurrentStock)
                .Take(RowLimit)
                .ToList();

            var model = new StockWarningModel
            {
                TotalItems = activeProducts.Count(),
                Stock = ordered
            };
            return View(model);
        }
    }

    public class StockWarningModel
    {
        public int TotalItems { get; set; }
        public List<Product> Stock { get; set; } = new();
    }
}
