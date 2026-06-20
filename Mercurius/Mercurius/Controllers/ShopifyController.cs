using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mercurius.Services;

namespace Mercurius.Controllers;

[Authorize]
public class ShopifyController : Controller
{
    private readonly IShopifyService _shopifyService;
    private readonly ILogger<ShopifyController> _logger;

    public ShopifyController(IShopifyService shopifyService, ILogger<ShopifyController> logger)
    {
        _shopifyService = shopifyService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var connected = await _shopifyService.TestConnectionAsync();
        ViewBag.Connected = connected;
        
        if (connected)
        {
            var products = await _shopifyService.GetProductsAsync();
            return View(products);
        }
        
        return View("ConnectionFailed");
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _shopifyService.GetProductsAsync();
        return Json(products);
    }

    [HttpGet]
    public async Task<IActionResult> GetProduct(string id)
    {
        var product = await _shopifyService.GetProductAsync(id);
        if (product == null)
            return NotFound();
        return Json(product);
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePrice(string productId, decimal price)
    {
        var success = await _shopifyService.UpdateProductPriceAsync(productId, price);
        if (!success)
            return BadRequest("Failed to update price");
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> TestConnection()
    {
        var connected = await _shopifyService.TestConnectionAsync();
        return Json(new { connected });
    }

    [HttpPost]
    public async Task<IActionResult> SyncProducts()
    {
        _logger.LogInformation("Starting Shopify product sync...");
        
        var result = await _shopifyService.SyncProductsAsync();
        
        if (result.Success)
        {
            TempData["Success"] = $"Sync completed: {result.Created} created, {result.Updated} updated";
        }
        else
        {
            TempData["Error"] = $"Sync completed with errors: {result.Created} created, {result.Updated} updated, {result.Failed} failed";
        }
        
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> SyncProductsStatus()
    {
        var connected = await _shopifyService.TestConnectionAsync();
        if (!connected)
            return Json(new { error = "Not connected to Shopify" });

        var products = await _shopifyService.GetProductsAsync();
        return Json(new { 
            shopifyCount = products.Count(),
            message = $"Found {products.Count()} products in Shopify"
        });
    }
}