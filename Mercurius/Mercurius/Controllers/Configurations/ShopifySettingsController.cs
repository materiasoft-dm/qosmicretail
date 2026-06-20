using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mercurius.Models;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Services;
using System.Threading;

namespace Mercurius.Controllers.Configurations;

[Authorize(Policy = Common.ModuleRegistry.Pages.CONFIG_SHOPIFY)]
public class ShopifySettingsController : Controller
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ShopifySettings _shopifySettings;
    private readonly ILogger<ShopifySettingsController> _logger;

    private const string STORE_URL_KEY = "Shopify.StoreUrl";
    private const string ACCESS_TOKEN_KEY = "Shopify.AccessToken";
    private const string API_VERSION_KEY = "Shopify.ApiVersion";
    private const string DEFAULT_API_VERSION = "2026-04";

    public ShopifySettingsController(
        IUnitOfWork unitOfWork,
        ShopifySettings shopifySettings,
        ILogger<ShopifySettingsController> logger)
    {
        _unitOfWork = unitOfWork;
        _shopifySettings = shopifySettings;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var model = await GetSettingsAsync(ct);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ShopifySettingsViewModel model, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!ModelState.IsValid)
            return View(model);

        var repo = _unitOfWork.Repository<AppSetting>();

        await SaveSettingAsync(repo, STORE_URL_KEY, model.StoreUrl, "Shopify Store URL", ct);
        await SaveSettingAsync(repo, ACCESS_TOKEN_KEY, model.AccessToken, "Shopify Admin API Access Token", ct);
        await SaveSettingAsync(repo, API_VERSION_KEY, model.ApiVersion ?? DEFAULT_API_VERSION, "Shopify API Version", ct);

        await _unitOfWork.SaveChangesAsync(ct);

        TempData["Success"] = "Shopify settings saved successfully.";
        _logger.LogInformation("Shopify settings updated by user {User}", User.Identity?.Name);

        // Update the in-memory settings for the current HTTP request
        _shopifySettings.StoreUrl = model.StoreUrl;
        _shopifySettings.AccessToken = model.AccessToken;
        _shopifySettings.ApiVersion = model.ApiVersion ?? DEFAULT_API_VERSION;

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> TestConnection(ShopifySettingsViewModel model, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Temporarily update settings for testing
        _shopifySettings.StoreUrl = model.StoreUrl;
        _shopifySettings.AccessToken = model.AccessToken;
        _shopifySettings.ApiVersion = model.ApiVersion ?? DEFAULT_API_VERSION;

        // Validate inputs before attempting to build a URL
        if (string.IsNullOrWhiteSpace(_shopifySettings.StoreUrl) ||
            string.IsNullOrWhiteSpace(_shopifySettings.AccessToken) ||
            string.IsNullOrWhiteSpace(_shopifySettings.ApiVersion))
        {
            return Json(new { success = false, message = "Store URL, Access Token, and API Version are all required." });
        }

        // Ensure StoreUrl has proper format
        var storeUrl = _shopifySettings.StoreUrl;
        
        // Remove protocol if present
        storeUrl = storeUrl.Replace("https://", "").Replace("http://", "");
        
        // If it doesn't already end with myshopify.com, add it
        if (!storeUrl.EndsWith("myshopify.com", StringComparison.OrdinalIgnoreCase))
        {
            // Remove trailing .com if present and add myshopify.com
            if (storeUrl.EndsWith(".com"))
                storeUrl = storeUrl.Substring(0, storeUrl.Length - 3);
            storeUrl = storeUrl + ".myshopify.com";
        }
        
        storeUrl = "https://" + storeUrl;

        _logger.LogInformation("Testing Shopify connection to: {StoreUrl}/admin/api/{ApiVersion}/graphql.json", storeUrl, _shopifySettings.ApiVersion);

        // Create a test HttpClient to verify credentials using GraphQL API
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("X-Shopify-Access-Token", _shopifySettings.AccessToken);
        httpClient.BaseAddress = new Uri($"{storeUrl}/admin/api/{_shopifySettings.ApiVersion}/");

        try
        {
            // Use GraphQL API to verify credentials - this is more reliable
            var graphqlQuery = @"{""query"": ""{ shop { name } }""}";
            var content = new StringContent(graphqlQuery, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("graphql.json", content, ct);
            
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Connection successful!" });
            }
            else
            {
                // Try to get more details from error response
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                var errorMessage = string.IsNullOrEmpty(errorContent) 
                    ? response.StatusCode.ToString() 
                    : $"Connection failed: {response.StatusCode} - {errorContent}";
                return Json(new { success = false, message = errorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Shopify connection test failed");
            return Json(new { success = false, message = $"Connection failed: {ex.Message}" });
        }
    }

    private async Task<ShopifySettingsViewModel> GetSettingsAsync(CancellationToken ct)
    {
        var repo = _unitOfWork.Repository<AppSetting>();
        var settings = await repo.GetAllAsync(ct);

        return new ShopifySettingsViewModel
        {
            StoreUrl = settings.FirstOrDefault(s => s.Key == STORE_URL_KEY)?.Value ?? _shopifySettings.StoreUrl,
            AccessToken = settings.FirstOrDefault(s => s.Key == ACCESS_TOKEN_KEY)?.Value ?? _shopifySettings.AccessToken,
            ApiVersion = settings.FirstOrDefault(s => s.Key == API_VERSION_KEY)?.Value ?? _shopifySettings.ApiVersion ?? DEFAULT_API_VERSION
        };
    }

    private async Task SaveSettingAsync(
        IRepository<AppSetting> repo,
        string key,
        string value,
        string? description,
        CancellationToken ct)
    {
        var existing = (await repo.FindAsync(s => s.Key == key, ct)).FirstOrDefault();

        if (existing != null)
        {
            existing.Value = value;
            existing.UpdateDate = DateTime.UtcNow;
            await repo.UpdateAsync(existing, ct);
        }
        else
        {
            await repo.AddAsync(new AppSetting
            {
                Key = key,
                Value = value,
                Description = description,
                CreateDate = DateTime.UtcNow
            }, ct);
        }
    }
}