using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Mercurius.Shared.Products;
using Microsoft.AspNetCore.Components.Forms;

namespace Mercurius.Client.Services
{
    public class ProductsApiClient
    {
        public const long MaxImageBytes = 5 * 1024 * 1024;
        public const long MaxImportBytes = 20 * 1024 * 1024;

        private readonly HttpClient _http;

        public ProductsApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ProductListResultDto> GetAsync(string? search, int page, int pageSize, int? categoryId = null,
            string? sort = null, bool desc = false, CancellationToken ct = default)
        {
            var url = $"api/products?search={Uri.EscapeDataString(search ?? string.Empty)}&page={page}&pageSize={pageSize}";
            if (categoryId.HasValue && categoryId.Value > 0) url += $"&categoryId={categoryId.Value}";
            if (!string.IsNullOrEmpty(sort)) url += $"&sort={Uri.EscapeDataString(sort)}&desc={(desc ? "true" : "false")}";
            var result = await _http.GetFromJsonAsync<ProductListResultDto>(url, ct);
            return result ?? new ProductListResultDto();
        }

        public async Task<ProductDetailDto?> GetDetailAsync(int id)
        {
            var response = await _http.GetAsync($"api/products/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProductDetailDto>();
        }

        public async Task<List<Mercurius.Shared.Configuration.ProductCategoryDto>> GetCategoryOptionsAsync() =>
            await _http.GetFromJsonAsync<List<Mercurius.Shared.Configuration.ProductCategoryDto>>("api/products/categories") ?? new();

        public async Task<List<ProductCustomFieldDto>> GetCategoryFieldsAsync(int categoryId, int? productId = null)
        {
            var url = $"api/products/category-fields/{categoryId}";
            if (productId.HasValue && productId.Value > 0) url += $"?productId={productId.Value}";
            return await _http.GetFromJsonAsync<List<ProductCustomFieldDto>>(url) ?? new();
        }

        public async Task<(bool Success, string? Error)> UpdateAsync(int id, UpdateProductRequest request)
        {
            var response = await _http.PutAsJsonAsync($"api/products/{id}", request);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await ReadErrorAsync(response, "Failed to save product."));
        }

        public async Task<(int? Id, string? Error)> CreateAsync(CreateProductRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/products", request);
            if (!response.IsSuccessStatusCode) return (null, await ReadErrorAsync(response, "Failed to create product."));
            var body = await response.Content.ReadFromJsonAsync<CreatedIdDto>();
            return (body?.Id, null);
        }

        // Takes the bytes rather than the IBrowserFile so a photo picked on the Add form can be
        // held until the product exists (the upload needs its id) without depending on the
        // file input still being alive.
        public async Task<(string? ImageUrl, string? Error)> UploadPhotoAsync(int id, byte[] bytes, string fileName, string? contentType)
        {
            if (bytes.LongLength > MaxImageBytes) return (null, "The image must be 5 MB or smaller.");

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType);
            content.Add(fileContent, "file", fileName);

            var response = await _http.PostAsync($"api/products/{id}/photo", content);
            if (!response.IsSuccessStatusCode) return (null, await ReadErrorAsync(response, "Failed to upload the photo."));
            var body = await response.Content.ReadFromJsonAsync<PhotoUploadedDto>();
            return (body?.ImageUrl, null);
        }

        public async Task<(string? JobId, string? Error)> StartImportAsync(IBrowserFile file, bool updateExisting)
        {
            if (file.Size > MaxImportBytes) return (null, "The file must be 20 MB or smaller.");

            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(file.OpenReadStream(MaxImportBytes));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(fileContent, "csvFile", file.Name);
            content.Add(new StringContent(updateExisting ? "true" : "false"), "updateExisting");

            var response = await _http.PostAsync("api/products/import", content);
            if (!response.IsSuccessStatusCode) return (null, await ReadErrorAsync(response, "Failed to start the import."));
            var body = await response.Content.ReadFromJsonAsync<ProductImportStartedDto>();
            return (body?.JobId, null);
        }

        // Null when the job is unknown — the tracker is in-memory, so a server restart forgets it.
        public async Task<ProductImportStatusDto?> GetImportStatusAsync(string jobId)
        {
            var response = await _http.GetAsync($"api/products/import/{Uri.EscapeDataString(jobId)}");
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProductImportStatusDto>();
        }

        private static async Task<string> ReadErrorAsync(HttpResponseMessage response, string fallback)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden) return "You don't have permission to do that.";
            try
            {
                var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                if (body != null && body.TryGetValue("error", out var e) && e != null) return e.ToString() ?? fallback;
            }
            catch
            {
                // Not a JSON error body (e.g. a 413 from the server's request size limit).
            }
            return response.StatusCode == HttpStatusCode.RequestEntityTooLarge ? "The file is too large." : fallback;
        }

        private sealed class CreatedIdDto
        {
            public int Id { get; set; }
        }

        private sealed class PhotoUploadedDto
        {
            public string? ImageUrl { get; set; }
        }
    }
}
