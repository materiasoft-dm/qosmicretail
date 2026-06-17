using Microsoft.AspNetCore.Mvc;
using Mercurius.Common.BusinessModel;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.ViewComponents.Dashboard
{
    public class ItemsSoldTodayViewComponent : ViewComponent
    {
        private readonly IUnitOfWork _unitOfWork;

        public ItemsSoldTodayViewComponent(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var start = DateTime.Today;
            var end = start.AddDays(1).AddTicks(-1);

            var finalizedInvoices = await _unitOfWork.Repository<Invoice>()
                .FindAsync(invoice => invoice.StatusId == (int)StatusCollection.InvoiceStatus.Finalized
                                      && invoice.InvoiceDate >= start
                                      && invoice.InvoiceDate <= end);
            var finalizedInvoiceIds = finalizedInvoices.Select(invoice => invoice.Id).ToHashSet();

            var invoiceItems = await _unitOfWork.Repository<InvoiceItem>()
                .FindAsync(item => finalizedInvoiceIds.Contains(item.InvoiceId));

            var products = await _unitOfWork.Repository<Product>().GetAllAsync();
            var productsById = products.ToDictionary(product => product.Id);

            var soldItems = invoiceItems
                .Select(item =>
                {
                    productsById.TryGetValue(item.ProductId, out var product);
                    return new ProductSoldViewModel
                    {
                        ProductDisplayName = product == null
                            ? string.Empty
                            : $"{product.Name} ({product.PartCode})",
                        PartCode = product?.PartCode,
                        ItemName = product?.Name,
                        Quantity = item.Quantity
                    };
                })
                .ToList();

            return View(soldItems);
        }
    }
}
