using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Services;

public class ShopifySettings
{
    public string AccessToken { get; set; } = string.Empty;
    public string StoreUrl { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2026-04";
}

public interface IShopifyService
{
    Task<ShopifyProduct?> GetProductAsync(string productId);
    Task<IEnumerable<ShopifyProduct>> GetProductsAsync();
    Task<bool> UpdateProductPriceAsync(string productId, decimal price);
    Task<bool> TestConnectionAsync();
    Task<ShopifySyncResult> SyncProductsAsync(CancellationToken cancellationToken = default);
}

public class ShopifySyncResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool Success => Failed == 0;
}

public class ShopifyProduct
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// The price of the product variant (sale price in local db)
    /// </summary>
    public decimal? Price { get; set; }
    
    /// <summary>
    /// The original/cost price before sale (compare_at_price in Shopify)
    /// </summary>
    public decimal? CompareAtPrice { get; set; }
    
    /// <summary>
    /// The cost per item for the merchant (not visible in storefront)
    /// </summary>
    public decimal? CostPrice { get; set; }
    
    public string? Sku { get; set; }
    public string? Description { get; set; }
    
    /// <summary>
    /// Primary product image URL
    /// </summary>
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// All product images (URL, alt text, position)
    /// </summary>
    public List<ShopifyProductImage> Images { get; set; } = new();
    
    public int? InventoryQuantity { get; set; }
    public string Status { get; set; } = "active";
}

/// <summary>
/// Shopify product image with position and alt text
/// </summary>
public class ShopifyProductImage
{
    public long Id { get; set; }
    public string Src { get; set; } = string.Empty;
    public string? Alt { get; set; }
    public int Position { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class ShopifyService : IShopifyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ShopifySettings _settings;
    private readonly ILogger<ShopifyService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWebHostEnvironment _webHostEnvironment;

    // Keys used by the configuration screen (ShopifySettingsController) to persist
    // credentials to the AppSetting table. The DB is the single source of truth —
    // appsettings.json is no longer consulted for Shopify config.
    internal const string STORE_URL_KEY = "Shopify.StoreUrl";
    internal const string ACCESS_TOKEN_KEY = "Shopify.AccessToken";
    internal const string API_VERSION_KEY = "Shopify.ApiVersion";
    internal const string DEFAULT_API_VERSION = "2026-04";

    private static readonly HashSet<string> AllowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

    public ShopifyService(IHttpClientFactory httpClientFactory, ShopifySettings settings,
        ILogger<ShopifyService> logger, IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _webHostEnvironment = webHostEnvironment;
    }

    /// <summary>
    /// Returns a fresh HttpClient configured with the current Shopify credentials
    /// from the AppSetting table. We do NOT cache the BaseAddress / access-token
    /// header on a long-lived client because the configuration screen can update
    /// credentials at runtime and we want subsequent calls to pick them up
    /// immediately without an app restart.
    /// </summary>
    private async Task<HttpClient> CreateShopifyClientAsync(CancellationToken ct = default)
    {
        var (storeUrl, accessToken, apiVersion) = await GetShopifyCredentialsAsync(ct);

        var client = _httpClientFactory.CreateClient("Shopify");
        if (!client.DefaultRequestHeaders.Contains("X-Shopify-Access-Token"))
        {
            client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", accessToken);
        }
        else
        {
            client.DefaultRequestHeaders.Remove("X-Shopify-Access-Token");
            client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", accessToken);
        }
        client.BaseAddress = new Uri($"https://{storeUrl}/admin/api/{apiVersion}/");
        return client;
    }

    /// <summary>
    /// Reads Shopify credentials from the AppSetting table. Falls back to the
    /// in-memory ShopifySettings (which is hydrated from the DB at startup) and
    /// then to safe defaults. Never throws — returns empty strings if nothing is
    /// configured so callers can surface a friendly error.
    /// </summary>
    private async Task<(string StoreUrl, string AccessToken, string ApiVersion)> GetShopifyCredentialsAsync(CancellationToken ct)
    {
        try
        {
            var repo = _unitOfWork.Repository<AppSetting>();
            var all = await repo.GetAllAsync(ct);
            var dict = all.ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);

            var storeUrl = dict.TryGetValue(STORE_URL_KEY, out var u) && !string.IsNullOrWhiteSpace(u)
                ? u
                : (_settings?.StoreUrl ?? string.Empty);
            var accessToken = dict.TryGetValue(ACCESS_TOKEN_KEY, out var t) && !string.IsNullOrWhiteSpace(t)
                ? t
                : (_settings?.AccessToken ?? string.Empty);
            var apiVersion = dict.TryGetValue(API_VERSION_KEY, out var v) && !string.IsNullOrWhiteSpace(v)
                ? v
                : (!string.IsNullOrWhiteSpace(_settings?.ApiVersion) ? _settings.ApiVersion : DEFAULT_API_VERSION);

            return (NormalizeStoreUrl(storeUrl), accessToken, apiVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read Shopify settings from AppSetting table; falling back to in-memory defaults");
            return (
                NormalizeStoreUrl(_settings?.StoreUrl ?? string.Empty),
                _settings?.AccessToken ?? string.Empty,
                string.IsNullOrWhiteSpace(_settings?.ApiVersion) ? DEFAULT_API_VERSION : _settings.ApiVersion);
        }
    }

    /// <summary>
    /// Normalizes a user-entered store URL to the canonical
    /// "store.myshopify.com" host (no protocol, no trailing slash).
    /// </summary>
    private static string NormalizeStoreUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var s = raw.Trim().Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                          .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
                          .TrimEnd('/');
        if (!s.EndsWith("myshopify.com", StringComparison.OrdinalIgnoreCase))
        {
            if (s.EndsWith(".com", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - 4);
            s = s + ".myshopify.com";
        }
        return s;
    }

    public async Task<ShopifyProduct?> GetProductAsync(string productId)
    {
        try
        {
            var http = await CreateShopifyClientAsync();
            var response = await http.GetAsync($"products/{productId}.json");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadFromJsonAsync<ShopifyProductListResponse>();
            return MapToShopifyProduct(content?.Product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Shopify product {ProductId}", productId);
            return null;
        }
    }

    public async Task<IEnumerable<ShopifyProduct>> GetProductsAsync()
    {
        try
        {
            var http = await CreateShopifyClientAsync();
            var response = await http.GetAsync("products.json");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadFromJsonAsync<ShopifyProductsListResponse>();
            var products = content?.Products
                .Select(MapToShopifyProduct)
                .Where(p => p != null)
                .Cast<ShopifyProduct>()
                .ToList() ?? new List<ShopifyProduct>();
            return products;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Shopify products");
            return Enumerable.Empty<ShopifyProduct>();
        }
    }

    /// <summary>
    /// Maps Shopify API response to ShopifyProduct model
    /// </summary>
    private ShopifyProduct? MapToShopifyProduct(ShopifyProductResponse? shopifyResponse)
    {
        if (shopifyResponse == null) return null;

        // Get the first variant for pricing info
        var variant = shopifyResponse.Variants?.FirstOrDefault();

        // Map all images (sorted by position)
        var images = shopifyResponse.Images?
            .OrderBy(i => i.Position)
            .Select(i => new ShopifyProductImage
            {
                Id = i.Id,
                Src = i.Src,
                Alt = i.Alt,
                Position = i.Position,
                Width = i.Width,
                Height = i.Height
            })
            .ToList() ?? new List<ShopifyProductImage>();

        return new ShopifyProduct
        {
            Id = shopifyResponse.Id.ToString(),
            Title = shopifyResponse.Title,
            Price = variant?.Price,
            CompareAtPrice = variant?.CompareAtPrice,
            CostPrice = variant?.CostPrice,
            Sku = variant?.Sku,
            Description = shopifyResponse.BodyHtml,
            ImageUrl = shopifyResponse.ImageSrc,
            Images = images,
            InventoryQuantity = variant?.InventoryQuantity,
            Status = shopifyResponse.Status
        };
    }

    /// <summary>
    /// Serializes additional product images (excluding the primary) to a JSON string for storage.
    /// The primary image is stored in ImageFilename; this stores the rest.
    /// </summary>
    private static string? SerializeAdditionalImages(List<ShopifyProductImage> images)
    {
        if (images == null || images.Count <= 1) return null;

        var additional = images.Skip(1).Select(i => new
        {
            i.Src,
            i.Alt,
            i.Position,
            i.Width,
            i.Height
        }).ToList();

        return System.Text.Json.JsonSerializer.Serialize(additional);
    }

    public async Task<bool> UpdateProductPriceAsync(string productId, decimal price)
    {
        try
        {
            var payload = new
            {
                product = new
                {
                    id = productId,
                    variants = new[]
                    {
                        new { id = productId, price = price.ToString("F2") }
                    }
                }
            };

            var http = await CreateShopifyClientAsync();
            var response = await http.PutAsJsonAsync($"products/{productId}.json", payload);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Shopify product {ProductId} price", productId);
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var http = await CreateShopifyClientAsync();
            var response = await http.GetAsync("shop.json");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Shopify");
            return false;
        }
    }

    public async Task<ShopifySyncResult> SyncProductsAsync(CancellationToken cancellationToken = default)
    {
        var result = new ShopifySyncResult();

        try
        {
            var shopifyProducts = await GetProductsAsync();
            var productList = shopifyProducts.ToList();

            _logger.LogInformation("Starting Shopify sync: {Count} products found", productList.Count);

            var productRepo = _unitOfWork.Repository<Product>();
            var imageRepo = _unitOfWork.Repository<ProductImage>();

            foreach (var shopifyProduct in productList)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    // Try to find existing product by SKU
                    var existingProducts = await productRepo.FindAsync(p => p.PartCode == shopifyProduct.Sku);
                    var existing = existingProducts.FirstOrDefault();

                    // Download all images locally and build ProductImage entities
                    var downloadedImages = await DownloadImagesAsync(shopifyProduct.Images, cancellationToken);

                    if (existing != null)
                    {
                        // Update existing product
                        existing.Name = shopifyProduct.Title;
                        existing.Description = shopifyProduct.Description ?? existing.Description;
                        existing.CurrentSalePrice = shopifyProduct.Price ?? existing.CurrentSalePrice;
                        existing.CurrentCostPrice = shopifyProduct.CostPrice ?? existing.CurrentCostPrice;

                        // Primary image: first downloaded image (or keep existing)
                        if (downloadedImages.Count > 0)
                        {
                            existing.ImageFilename = downloadedImages[0].Filename;
                        }
                        else if (!string.IsNullOrEmpty(shopifyProduct.ImageUrl))
                        {
                            existing.ImageFilename = shopifyProduct.ImageUrl;
                        }

                        existing.UpdatedDate = DateTime.UtcNow;

                        // Calculate markup if we have both cost and sale price
                        if (shopifyProduct.CostPrice.HasValue && shopifyProduct.Price.HasValue && shopifyProduct.CostPrice > 0)
                        {
                            existing.MarkUpPercentage = ((shopifyProduct.Price.Value - shopifyProduct.CostPrice.Value) / shopifyProduct.CostPrice.Value) * 100;
                        }

                        // Replace existing ProductImages with the freshly downloaded set
                        var existingImages = await imageRepo.FindAsync(i => i.ProductId == existing.Id);
                        foreach (var oldImg in existingImages)
                        {
                            await imageRepo.DeleteAsync(oldImg.Id);
                            TryDeleteLocalImage(oldImg.Filename);
                        }

                        for (int i = 0; i < downloadedImages.Count; i++)
                        {
                            var di = downloadedImages[i];
                            di.ProductId = existing.Id;
                            // Position 0 is reserved for the primary; additional images start at 1
                            di.Position = i;
                            await imageRepo.AddAsync(di);
                        }

                        await productRepo.UpdateAsync(existing);
                        result.Updated++;
                        _logger.LogDebug("Updated product: {Sku} - {Title} ({ImageCount} images)", shopifyProduct.Sku, shopifyProduct.Title, downloadedImages.Count);
                    }
                    else
                    {
                        // Calculate markup
                        decimal markup = 0;
                        if (shopifyProduct.CostPrice.HasValue && shopifyProduct.Price.HasValue && shopifyProduct.CostPrice > 0)
                        {
                            markup = ((shopifyProduct.Price.Value - shopifyProduct.CostPrice.Value) / shopifyProduct.CostPrice.Value) * 100;
                        }

                        // Create new product
                        var newProduct = new Product
                        {
                            PartCode = shopifyProduct.Sku ?? shopifyProduct.Id,
                            Name = shopifyProduct.Title,
                            Description = shopifyProduct.Description ?? string.Empty,
                            CurrentSalePrice = shopifyProduct.Price ?? 0,
                            CurrentCostPrice = shopifyProduct.CostPrice,
                            ImageFilename = downloadedImages.Count > 0 ? downloadedImages[0].Filename : shopifyProduct.ImageUrl,
                            CurrentStock = shopifyProduct.InventoryQuantity ?? 0,
                            MarkUpPercentage = markup,
                            CreateDate = DateTime.UtcNow,
                            CreatedBy = Guid.Empty, // System user
                            IsActive = shopifyProduct.Status == "active",
                            LowStockCount = 0
                        };

                        await productRepo.AddAsync(newProduct);

                        // Attach downloaded images to the new product
                        for (int i = 0; i < downloadedImages.Count; i++)
                        {
                            var di = downloadedImages[i];
                            di.ProductId = newProduct.Id;
                            di.Position = i;
                            await imageRepo.AddAsync(di);
                        }

                        result.Created++;
                        _logger.LogDebug("Created product: {Sku} - {Title} ({ImageCount} images)", shopifyProduct.Sku, shopifyProduct.Title, downloadedImages.Count);
                    }
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add($"Product {shopifyProduct.Sku ?? shopifyProduct.Id}: {ex.Message}");
                    _logger.LogError(ex, "Failed to sync product {ProductId}", shopifyProduct.Id);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Shopify sync completed: {Created} created, {Updated} updated, {Failed} failed",
                result.Created, result.Updated, result.Failed);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Shopify sync failed");
            result.Errors.Add($"Sync failed: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Downloads each Shopify image to wwwroot/ProductImages and returns a list of
    /// ProductImage entities (without ProductId/Position set — caller assigns those).
    /// Images that fail to download are skipped with a warning.
    /// </summary>
    private async Task<List<ProductImage>> DownloadImagesAsync(
        List<ShopifyProductImage> images, CancellationToken cancellationToken)
    {
        var result = new List<ProductImage>();
        if (images == null || images.Count == 0) return result;

        var folder = Path.Combine(_webHostEnvironment.WebRootPath, "ProductImages");
        Directory.CreateDirectory(folder);

        foreach (var src in images.OrderBy(i => i.Position))
        {
            if (string.IsNullOrWhiteSpace(src.Src)) continue;

            try
            {
                var ext = GuessImageExtension(src.Src);
                if (!AllowedImageExtensions.Contains(ext))
                {
                    _logger.LogWarning("Skipping Shopify image with unsupported extension: {Src}", src.Src);
                    continue;
                }

                var fileName = $"{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(folder, fileName);

                // Use a plain HttpClient for image downloads (no auth header needed —
                // Shopify CDN URLs are public). This avoids mutating the named
                // "Shopify" client's headers for every image.
                using var imgHttp = new HttpClient();
                using var imgResp = await imgHttp.GetAsync(src.Src, cancellationToken);
                if (!imgResp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to download Shopify image {Src}: HTTP {Status}",
                        src.Src, (int)imgResp.StatusCode);
                    continue;
                }

                await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                {
                    await imgResp.Content.CopyToAsync(fs, cancellationToken);
                }

                result.Add(new ProductImage
                {
                    Filename = fileName,
                    IsLocal = true,
                    Alt = src.Alt,
                    CreateDate = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download Shopify image {Src}", src.Src);
            }
        }

        return result;
    }

    /// <summary>
    /// Best-effort guess of an image file extension from a URL. Falls back to .jpg.
    /// </summary>
    private static string GuessImageExtension(string url)
    {
        try
        {
            var path = new Uri(url).AbsolutePath;
            var ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext)) return ext.ToLowerInvariant();
        }
        catch
        {
            // ignore — fall through to default
        }
        return ".jpg";
    }

    /// <summary>
    /// Best-effort delete of a previously downloaded local image file.
    /// </summary>
    private void TryDeleteLocalImage(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return;
        try
        {
            var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, "ProductImages", filename);
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete local image file {Filename}", filename);
        }
    }

    private class ShopifyResponse
    {
        public ShopifyProduct? Product { get; set; }
    }

    private class ShopifyProductsResponse
    {
        public IEnumerable<ShopifyProduct> Products { get; set; } = Enumerable.Empty<ShopifyProduct>();
    }

    /// <summary>
    /// Shopify API response for a product with nested variants and images
    /// </summary>
    private class ShopifyProductResponse
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? BodyHtml { get; set; }
        public string Status { get; set; } = "active";
        public string? ImageSrc { get; set; }
        public IEnumerable<ShopifyVariantResponse>? Variants { get; set; }
        public IEnumerable<ShopifyImageResponse>? Images { get; set; }
    }

    /// <summary>
    /// Shopify API response for a product variant (contains pricing)
    /// </summary>
    private class ShopifyVariantResponse
    {
        public long Id { get; set; }
        public string? Sku { get; set; }
        public decimal? Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public decimal? CostPrice { get; set; }
        public int? InventoryQuantity { get; set; }
    }

    /// <summary>
    /// Shopify API response for a product image
    /// </summary>
    private class ShopifyImageResponse
    {
        public long Id { get; set; }
        public string Src { get; set; } = string.Empty;
        public string? Alt { get; set; }
        public int Position { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// Wrapper for products list response
    /// </summary>
    private class ShopifyProductsListResponse
    {
        public IEnumerable<ShopifyProductResponse> Products { get; set; } = Enumerable.Empty<ShopifyProductResponse>();
    }

    /// <summary>
    /// Wrapper for single product response
    /// </summary>
    private class ShopifyProductListResponse
    {
        public ShopifyProductResponse? Product { get; set; }
    }
}