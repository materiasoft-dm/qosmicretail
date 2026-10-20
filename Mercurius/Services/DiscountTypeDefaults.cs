using System;
using System.Collections.Generic;
using System.Linq;
using Mercurius.Repo.Models;
using Mercurius.Shared.Bir;

namespace Mercurius.Services
{
    /// <summary>
    /// The statutory discounts a brand-new tenant starts with, as DiscountType rows. Existing
    /// tenants got the same rows from the SeedDefaultDiscountTypes migration.
    /// </summary>
    public static class DiscountTypeDefaults
    {
        public static IEnumerable<DiscountType> For(int tenantId)
        {
            var now = DateTime.UtcNow;
            return DefaultDiscounts.Statutory.Select(r => new DiscountType
            {
                TenantId = tenantId,
                Name = r.Name,
                ShortLabel = r.ShortLabel,
                ValueKind = (int)r.ValueKind,
                Value = r.Value,
                IsVatExempt = r.IsVatExempt,
                RequiresCustomerId = r.RequiresCustomerId,
                IdLabel = r.IdLabel,
                Category = (int)r.Category,
                IsActive = true,
                CreatedDate = now,
                UpdatedDate = now
            });
        }
    }
}
