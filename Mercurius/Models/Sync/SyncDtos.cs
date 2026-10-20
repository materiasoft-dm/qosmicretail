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
        // Mercurius.Shared.Bir.VatType.
        public int VatType { get; set; }
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
        // Null from app builds that don't know about VAT classes: keep the server's value.
        public int? VatType { get; set; }
    }

    public class InvoiceItemSyncDto
    {
        public Guid SyncId { get; set; }
        public Guid ProductSyncId { get; set; }
        public decimal Quantity { get; set; }
        public decimal SalePrice { get; set; }
        public decimal CostPrice { get; set; }
        public string? Remarks { get; set; }
        // BIR fields, sent by tills that print BIR invoices (absent from older builds). NetAmount
        // is what the customer paid for the line after any special discount.
        public int? VatType { get; set; }
        public decimal VatAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal? NetAmount { get; set; }
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
        // Cash | Card. Null from app builds older than this field; the server then reads it from Notes.
        public string? PaymentMethod { get; set; }

        // ---- BIR invoice data (see Invoice). Absent from older app builds, which get a
        // server-assigned invoice number as before.
        // The terminal (from RegisterTerminal) and the serial number it printed on the invoice.
        public int? PosTerminalId { get; set; }
        public long? TerminalInvoiceSequence { get; set; }
        public decimal VatableSales { get; set; }
        public decimal VatAmount { get; set; }
        public decimal VatExemptSales { get; set; }
        public decimal ZeroRatedSales { get; set; }
        public decimal LessVat { get; set; }
        public decimal DiscountAmount { get; set; }
        public int? DiscountTypeId { get; set; }
        public string? DiscountName { get; set; }
        public int SpecialDiscountType { get; set; }
        public string? SpecialDiscountIdNumber { get; set; }
        public string? SpecialDiscountHolderName { get; set; }
        public string? SpecialDiscountHolderTin { get; set; }
        public string? BuyerName { get; set; }
        public string? BuyerTin { get; set; }
        public string? BuyerAddress { get; set; }
        public string? BuyerBusinessStyle { get; set; }
        public string? ReceiptText { get; set; }
        public string? JournalHash { get; set; }

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
        public int VatType { get; set; }
        public decimal VatAmount { get; set; }
        public decimal DiscountAmount { get; set; }
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
        public string? PaymentMethod { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal RefundedAmount { get; set; }
        public bool HasRefund { get; set; }
        public string Status { get; set; } = "";
        public DateTime LastModifiedUtc { get; set; }

        // BIR invoice data (zero/null on sales made before it existed, or on the web).
        public int? PosTerminalId { get; set; }
        public long? TerminalInvoiceSequence { get; set; }
        public decimal VatableSales { get; set; }
        public decimal VatAmount { get; set; }
        public decimal VatExemptSales { get; set; }
        public decimal ZeroRatedSales { get; set; }
        public decimal LessVat { get; set; }
        public decimal DiscountAmount { get; set; }
        public int? DiscountTypeId { get; set; }
        public string? DiscountName { get; set; }
        public int SpecialDiscountType { get; set; }
        public string? SpecialDiscountIdNumber { get; set; }
        public string? SpecialDiscountHolderName { get; set; }
        public string? SpecialDiscountHolderTin { get; set; }
        public string? BuyerName { get; set; }
        public string? BuyerTin { get; set; }
        public string? BuyerAddress { get; set; }
        public string? BuyerBusinessStyle { get; set; }
        public string? ReceiptText { get; set; }
        public string? JournalHash { get; set; }

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

    // ---- BIR terminal registration (Api/BirSyncController)

    public class TerminalRegisterRequest
    {
        // The device's per-install id (also sent as X-Device-Id on every request).
        public string DeviceId { get; set; } = "";
        public string? Name { get; set; }
        public int LocationId { get; set; }
    }

    // Everything a till needs to print BIR invoices and readings: who it is, the branch it sells
    // for, the software's accreditation, and its counters as the server last knew them.
    public class TerminalConfigDto
    {
        public int TerminalId { get; set; }
        public string TerminalCode { get; set; } = "";
        public string? Name { get; set; }
        public string? MachineIdentificationNumber { get; set; }
        public string? SerialNumber { get; set; }
        public string? PermitToUseNumber { get; set; }
        public DateTime? PermitToUseDateIssued { get; set; }
        public long LastInvoiceSequence { get; set; }
        public int LastZCounter { get; set; }
        public decimal AccumulatedGrandTotal { get; set; }
        public int ResetCounter { get; set; }

        public int? LocationId { get; set; }
        public string? SellerRegisteredName { get; set; }
        public string? SellerBusinessName { get; set; }
        public string? SellerAddress { get; set; }
        public string? SellerTin { get; set; }
        public string? SellerBranchCode { get; set; }
        public bool SellerIsVatRegistered { get; set; } = true;

        public string? SupplierName { get; set; }
        public string? SupplierAddress { get; set; }
        public string? SupplierTin { get; set; }
        public string? AccreditationNumber { get; set; }
        public DateTime? AccreditationDateIssued { get; set; }
        public DateTime? AccreditationValidUntil { get; set; }

        // Whether the signed-in user may change the BIR registration fields below.
        public bool CanEditRegistration { get; set; }
    }

    public class TerminalRegistrationUpdate
    {
        public int TerminalId { get; set; }
        public string? MachineIdentificationNumber { get; set; }
        public string? SerialNumber { get; set; }
        public string? PermitToUseNumber { get; set; }
        public DateTime? PermitToUseDateIssued { get; set; }
    }

    public class ZReadingPushDto
    {
        public Guid SyncId { get; set; }
        public int PosTerminalId { get; set; }
        public int ZCounter { get; set; }
        public int ResetCounter { get; set; }
        public DateTime ReadingDate { get; set; }
        public int SalesCount { get; set; }
        public long BeginningInvoiceSequence { get; set; }
        public long EndingInvoiceSequence { get; set; }
        public decimal GrossSales { get; set; }
        public decimal LessVat { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal SeniorCitizenDiscount { get; set; }
        public decimal PwdDiscount { get; set; }
        public decimal SoloParentDiscount { get; set; }
        public decimal NationalAthleteDiscount { get; set; }
        public decimal MedalOfValorDiscount { get; set; }
        public decimal NetSales { get; set; }
        public decimal VatableSales { get; set; }
        public decimal VatAmount { get; set; }
        public decimal VatExemptSales { get; set; }
        public decimal ZeroRatedSales { get; set; }
        public decimal CashSales { get; set; }
        public decimal CardSales { get; set; }
        public decimal PreviousGrandTotal { get; set; }
        public decimal NewGrandTotal { get; set; }
        public string? ReportText { get; set; }
    }
}
