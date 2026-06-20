using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.ViewComponents.Dashboard;

/// <summary>
/// Dashboard widget showing medicines expiring within 90 days,
/// ordered by nearest expiry first. Includes expired count.
/// </summary>
public class ExpiryAlertsViewComponent : ViewComponent
{
    private readonly IUnitOfWork _unitOfWork;

    public ExpiryAlertsViewComponent(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var now = DateTime.UtcNow;
        var ninetyDays = now.AddDays(90);

        var batches = await _unitOfWork.Repository<MedicineBatch>()
            .FindAsync(b => b.IsActive && b.ExpiryDate <= ninetyDays);

        var productIds = batches.Select(b => b.ProductId).Distinct().ToList();
        var products = await _unitOfWork.Repository<Product>()
            .FindAsync(p => productIds.Contains(p.Id));
        var prodDict = products.ToDictionary(p => p.Id);

        var model = new ExpiryAlertsViewModel
        {
            ExpiredCount = batches.Count(b => b.ExpiryDate < now),
            ExpiringWithin30Days = batches.Count(b => b.ExpiryDate >= now && b.ExpiryDate <= now.AddDays(30)),
            ExpiringWithin90Days = batches.Count(b => b.ExpiryDate > now.AddDays(30) && b.ExpiryDate <= ninetyDays),
            Items = batches.OrderBy(b => b.ExpiryDate).Take(10).Select(b => new ExpiryAlertItem
            {
                ProductName = prodDict.TryGetValue(b.ProductId, out var p) ? p.Name : "Unknown",
                BatchNumber = b.BatchNumber,
                ExpiryDate = b.ExpiryDate,
                RemainingQuantity = b.RemainingQuantity
            }).ToList()
        };

        return View(model);
    }
}

public class ExpiryAlertsViewModel
{
    public int ExpiredCount { get; set; }
    public int ExpiringWithin30Days { get; set; }
    public int ExpiringWithin90Days { get; set; }
    public List<ExpiryAlertItem> Items { get; set; } = new();
}

public class ExpiryAlertItem
{
    public string ProductName { get; set; } = "";
    public string BatchNumber { get; set; } = "";
    public DateTime ExpiryDate { get; set; }
    public decimal RemainingQuantity { get; set; }
}
