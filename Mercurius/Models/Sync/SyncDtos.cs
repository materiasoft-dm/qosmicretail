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
        public string? Barcode { get; set; }
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
        public string? Barcode { get; set; }
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
        // Who rang the sale up on the device — can differ from the pushing user when sales were
        // left queued across a logout. Only trusted after SyncController verifies it's a real
        // user in the pushing user's own tenant; otherwise the pushing user is recorded.
        public string? CashierUserId { get; set; }
        public List<InvoiceItemSyncDto> Items { get; set; } = new();
    }

    // A pharmacy branch a mobile device can be assigned to (Mercurius.Mobile's Settings page) —
    // see Api/SyncController.GetLocations. Deliberately just {Id, Name}: the device only needs
    // enough to let the cashier pick one and to send back with every invoice push.
    public class LocationSyncDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    // ---- Receipts pull (api/sync/receipts/pull) — display-ready sales for the mobile Receipts
    // page, from every terminal and the web. Unlike InvoiceSyncDto (a raw record), this carries
    // everything a list/detail view needs, so the device never has to join anything itself.

    public class ReceiptLineSyncDto
    {
        public Guid SyncId { get; set; }
        public Guid ProductSyncId { get; set; }
        public string ProductName { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal SalePrice { get; set; }
        public decimal LineTotal { get; set; }
        public decimal RefundedQuantity { get; set; }
    }

    public class ReceiptSyncDto
    {
        public Guid SyncId { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public DateTime InvoiceDate { get; set; }
        public int LocationId { get; set; }
        public string? LocationName { get; set; }
        public string? CashierName { get; set; }
        public string? SourceChannel { get; set; }
        public string? SourceDeviceId { get; set; }
        public string? SourceDeviceName { get; set; }
        public string? Notes { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal RefundedAmount { get; set; }
        public bool HasRefund { get; set; }
        public string Status { get; set; } = "";
        public DateTime LastModifiedUtc { get; set; }
        public List<ReceiptLineSyncDto> Items { get; set; } = new();
    }

    public class ReceiptPullPage
    {
        public List<ReceiptSyncDto> Items { get; set; } = new();
        // Opaque; pass back as `cursor` to continue. Null only if nothing has ever been returned.
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; }
    }

    public class SyncPushResultItem
    {
        public Guid SyncId { get; set; }
        public bool Created { get; set; }
        public DateTime LastModifiedUtc { get; set; }
        public string? Error { get; set; }
    }
}
