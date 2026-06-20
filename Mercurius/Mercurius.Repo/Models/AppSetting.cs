using System;

namespace Mercurius.Repo.Models;

public class AppSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The setting key (e.g., "Shopify.StoreUrl", "Shopify.AccessToken")
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The setting value
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Optional description for admin reference
    /// </summary>
    public string? Description { get; set; }

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdateDate { get; set; }
}