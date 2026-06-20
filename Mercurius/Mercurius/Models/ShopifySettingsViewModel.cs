namespace Mercurius.Models;

public class ShopifySettingsViewModel
{
    public string StoreUrl { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string? ApiVersion { get; set; }
}