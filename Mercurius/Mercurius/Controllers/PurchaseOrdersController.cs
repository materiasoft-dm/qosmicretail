using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers
{
    [Authorize]
    public class PurchaseOrdersController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public PurchaseOrdersController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        [Authorize(Policy = Common.ModuleRegistry.Pages.PURCHASE_ORDERS_LIST)]
        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var orders = await _unitOfWork.Repository<PurchaseOrder>().GetAllAsync(ct);
            var suppliers = await _unitOfWork.Repository<Supplier>().GetAllAsync(ct);
            ViewBag.Suppliers = suppliers.ToDictionary(s => s.Id, s => s.Name);
            return View(orders.OrderByDescending(o => o.OrderDate));
        }

        [Authorize(Policy = Common.ModuleRegistry.Pages.PURCHASE_ORDERS_CREATE)]
        public async Task<IActionResult> Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await LoadViewData(ct);
            return View();
        }

        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.PURCHASE_ORDERS_CREATE)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseOrder order, int[] productIds, decimal[] quantities, decimal[] costs, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (!ModelState.IsValid)
            {
                await LoadViewData(ct);
                return View(order);
            }

            order.OrderNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
            order.OrderDate = DateTime.UtcNow;
            order.Status = "PendingApproval";
            order.CreatedDate = DateTime.UtcNow;

            await _unitOfWork.Repository<PurchaseOrder>().AddAsync(order, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            for (int i = 0; i < productIds.Length; i++)
            {
                if (productIds[i] > 0 && quantities[i] > 0)
                {
                    await _unitOfWork.Repository<PurchaseOrderItem>().AddAsync(new PurchaseOrderItem
                    {
                        PurchaseOrderId = order.Id,
                        ProductId = productIds[i],
                        Quantity = quantities[i],
                        EstimatedUnitCost = costs.Length > i && costs[i] > 0 ? costs[i] : null
                    }, ct);
                }
            }
            await _unitOfWork.SaveChangesAsync(ct);

            TempData["Message"] = $"Purchase Order {order.OrderNumber} created.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = Common.ModuleRegistry.Pages.PURCHASE_ORDERS_EDIT)]
        public async Task<IActionResult> Edit(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var order = await _unitOfWork.Repository<PurchaseOrder>().GetByIdAsync(id.Value, ct);
            if (order == null) return NotFound();

            var items = await _unitOfWork.Repository<PurchaseOrderItem>()
                .FindAsync(poi => poi.PurchaseOrderId == order.Id, ct);
            ViewBag.Items = items.ToList();

            var products = await _unitOfWork.Repository<Product>().GetAllAsync(ct);
            ViewBag.Products = products.ToDictionary(p => p.Id, p => p.Name);

            await LoadViewData(ct);
            return View(order);
        }

        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.PURCHASE_ORDERS_APPROVE)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var order = await _unitOfWork.Repository<PurchaseOrder>().GetByIdAsync(id, ct);
            if (order != null && order.Status == "PendingApproval")
            {
                order.Status = "Approved";
                order.ApprovedDate = DateTime.UtcNow;
                order.UpdatedDate = DateTime.UtcNow;
                await _unitOfWork.Repository<PurchaseOrder>().UpdateAsync(order, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                TempData["Message"] = $"Order {order.OrderNumber} approved.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.PURCHASE_ORDERS_EDIT)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkSent(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var order = await _unitOfWork.Repository<PurchaseOrder>().GetByIdAsync(id, ct);
            if (order != null && order.Status == "Approved")
            {
                order.Status = "OrderSent";
                order.SentDate = DateTime.UtcNow;
                order.UpdatedDate = DateTime.UtcNow;
                await _unitOfWork.Repository<PurchaseOrder>().UpdateAsync(order, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                TempData["Message"] = $"Order {order.OrderNumber} marked as sent.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.PURCHASE_ORDERS_EDIT)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReceived(int id, bool isComplete, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var order = await _unitOfWork.Repository<PurchaseOrder>().GetByIdAsync(id, ct);
            if (order != null && order.Status == "OrderSent")
            {
                order.Status = isComplete ? "ReceivedComplete" : "ReceivedIncomplete";
                order.UpdatedDate = DateTime.UtcNow;
                await _unitOfWork.Repository<PurchaseOrder>().UpdateAsync(order, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                TempData["Message"] = $"Order {order.OrderNumber} marked as received {(isComplete ? "complete" : "incomplete")}.";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadViewData(CancellationToken ct = default)
        {
            var suppliers = await _unitOfWork.Repository<Supplier>().GetAllAsync(ct);
            ViewBag.SupplierId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                suppliers.Where(s => s.IsActive).OrderBy(s => s.Name), "Id", "Name");

            var products = await _unitOfWork.Repository<Product>().GetAllAsync(ct);
            ViewBag.ProductList = products.Where(p => p.IsActive).OrderBy(p => p.Name).ToList();
        }
    }
}
