using System;
using System.Collections.Generic;

namespace Mercurius.Models.Sync
{
    // Shapes for the mobile sync API (Api/SyncController). Devices are identified to each other
    // only by SyncId — never by the server's int Id, which has no meaning outside this database.

    public class ProductSyncDto
    {
        public Guid SyncId { get; set; }
        public string ProductCode { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? CategoryName { get; set; }
        public decimal? CurrentCostPrice { get; set; }
        public decimal? CurrentSalePrice { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal LowStockCount { get; set; }
        public decimal MarkUpPercentage { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastModifiedUtc { get; set; }
    }

    public class ProductPushDto
    {
        public Guid SyncId { get; set; }
        public string ProductCode { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? CategoryName { get; set; }
        public decimal? CurrentCostPrice { get; set; }
        public decimal? CurrentSalePrice { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal LowStockCount { get; set; }
        public decimal MarkUpPercentage { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class InvoiceItemSyncDto
    {
        public Guid SyncId { get; set; }
        public Guid ProductSyncId { get; set; }
        public decimal Quantity { get; set; }
        public decimal SalePrice { get; set; }
        public decimal CostPrice { get; set; }
        public string? Remarks { get; set; }
    }

    public class InvoiceSyncDto
    {
        public Guid SyncId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public int? CustomerId { get; set; }
        public int LocationId { get; set; }
        public string? Notes { get; set; }
        public decimal PaidAmount { get; set; }
        public bool HasRefund { get; set; }
        public DateTime LastModifiedUtc { get; set; }
        public List<InvoiceItemSyncDto> Items { get; set; } = new();
    }

    public class InvoicePushDto
    {
        public Guid SyncId { get; set; }
        // The real-world sale date/time, as observed by the device — trusted from the client,
        // unlike LastModified/CreatedDate bookkeeping fields, which the server always stamps
        // itself (see SyncController for the last-write-wins rationale).
        public DateTime InvoiceDate { get; set; }
        public int? CustomerId { get; set; }
        public int LocationId { get; set; }
        public string? Notes { get; set; }
        public decimal PaidAmount { get; set; }
        public List<InvoiceItemSyncDto> Items { get; set; } = new();
    }

    public class SyncPushResultItem
    {
        public Guid SyncId { get; set; }
        public bool Created { get; set; }
        public DateTime LastModifiedUtc { get; set; }
        public string? Error { get; set; }
    }
}
