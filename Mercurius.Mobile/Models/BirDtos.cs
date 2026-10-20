using System.Text.Json.Serialization;

namespace Mercurius.Mobile.Models;

// Mirror Mercurius/Models/Sync/SyncDtos.cs's terminal registration and Z-reading shapes
// (Api/BirSyncController on the server).

public class TerminalRegisterRequest
{
    [JsonPropertyName("deviceId")] public string DeviceId { get; set; } = "";
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("locationId")] public int LocationId { get; set; }
}

public class TerminalConfigDto
{
    [JsonPropertyName("terminalId")] public int TerminalId { get; set; }
    [JsonPropertyName("terminalCode")] public string TerminalCode { get; set; } = "";
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("machineIdentificationNumber")] public string? MachineIdentificationNumber { get; set; }
    [JsonPropertyName("serialNumber")] public string? SerialNumber { get; set; }
    [JsonPropertyName("permitToUseNumber")] public string? PermitToUseNumber { get; set; }
    [JsonPropertyName("permitToUseDateIssued")] public DateTime? PermitToUseDateIssued { get; set; }
    [JsonPropertyName("lastInvoiceSequence")] public long LastInvoiceSequence { get; set; }
    [JsonPropertyName("lastZCounter")] public int LastZCounter { get; set; }
    [JsonPropertyName("accumulatedGrandTotal")] public decimal AccumulatedGrandTotal { get; set; }
    [JsonPropertyName("resetCounter")] public int ResetCounter { get; set; }
    [JsonPropertyName("locationId")] public int? LocationId { get; set; }
    [JsonPropertyName("sellerRegisteredName")] public string? SellerRegisteredName { get; set; }
    [JsonPropertyName("sellerBusinessName")] public string? SellerBusinessName { get; set; }
    [JsonPropertyName("sellerAddress")] public string? SellerAddress { get; set; }
    [JsonPropertyName("sellerTin")] public string? SellerTin { get; set; }
    [JsonPropertyName("sellerBranchCode")] public string? SellerBranchCode { get; set; }
    [JsonPropertyName("sellerIsVatRegistered")] public bool SellerIsVatRegistered { get; set; } = true;
    [JsonPropertyName("supplierName")] public string? SupplierName { get; set; }
    [JsonPropertyName("supplierAddress")] public string? SupplierAddress { get; set; }
    [JsonPropertyName("supplierTin")] public string? SupplierTin { get; set; }
    [JsonPropertyName("accreditationNumber")] public string? AccreditationNumber { get; set; }
    [JsonPropertyName("accreditationDateIssued")] public DateTime? AccreditationDateIssued { get; set; }
    [JsonPropertyName("accreditationValidUntil")] public DateTime? AccreditationValidUntil { get; set; }
    [JsonPropertyName("canEditRegistration")] public bool CanEditRegistration { get; set; }
}

public class TerminalRegistrationUpdate
{
    [JsonPropertyName("terminalId")] public int TerminalId { get; set; }
    [JsonPropertyName("machineIdentificationNumber")] public string? MachineIdentificationNumber { get; set; }
    [JsonPropertyName("serialNumber")] public string? SerialNumber { get; set; }
    [JsonPropertyName("permitToUseNumber")] public string? PermitToUseNumber { get; set; }
    [JsonPropertyName("permitToUseDateIssued")] public DateTime? PermitToUseDateIssued { get; set; }
}

public class ZReadingPushDto
{
    [JsonPropertyName("syncId")] public Guid SyncId { get; set; }
    [JsonPropertyName("posTerminalId")] public int PosTerminalId { get; set; }
    [JsonPropertyName("zCounter")] public int ZCounter { get; set; }
    [JsonPropertyName("resetCounter")] public int ResetCounter { get; set; }
    [JsonPropertyName("readingDate")] public DateTime ReadingDate { get; set; }
    [JsonPropertyName("salesCount")] public int SalesCount { get; set; }
    [JsonPropertyName("beginningInvoiceSequence")] public long BeginningInvoiceSequence { get; set; }
    [JsonPropertyName("endingInvoiceSequence")] public long EndingInvoiceSequence { get; set; }
    [JsonPropertyName("grossSales")] public decimal GrossSales { get; set; }
    [JsonPropertyName("lessVat")] public decimal LessVat { get; set; }
    [JsonPropertyName("discountTotal")] public decimal DiscountTotal { get; set; }
    [JsonPropertyName("seniorCitizenDiscount")] public decimal SeniorCitizenDiscount { get; set; }
    [JsonPropertyName("pwdDiscount")] public decimal PwdDiscount { get; set; }
    [JsonPropertyName("soloParentDiscount")] public decimal SoloParentDiscount { get; set; }
    [JsonPropertyName("nationalAthleteDiscount")] public decimal NationalAthleteDiscount { get; set; }
    [JsonPropertyName("medalOfValorDiscount")] public decimal MedalOfValorDiscount { get; set; }
    [JsonPropertyName("netSales")] public decimal NetSales { get; set; }
    [JsonPropertyName("vatableSales")] public decimal VatableSales { get; set; }
    [JsonPropertyName("vatAmount")] public decimal VatAmount { get; set; }
    [JsonPropertyName("vatExemptSales")] public decimal VatExemptSales { get; set; }
    [JsonPropertyName("zeroRatedSales")] public decimal ZeroRatedSales { get; set; }
    [JsonPropertyName("cashSales")] public decimal CashSales { get; set; }
    [JsonPropertyName("cardSales")] public decimal CardSales { get; set; }
    [JsonPropertyName("previousGrandTotal")] public decimal PreviousGrandTotal { get; set; }
    [JsonPropertyName("newGrandTotal")] public decimal NewGrandTotal { get; set; }
    [JsonPropertyName("reportText")] public string? ReportText { get; set; }
}

// Mercurius.Shared.Configuration.DiscountTypeDto (GET api/sync/discount-types).
public class DiscountTypeDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("shortLabel")] public string? ShortLabel { get; set; }
    [JsonPropertyName("valueKind")] public int ValueKind { get; set; }
    [JsonPropertyName("value")] public decimal Value { get; set; }
    [JsonPropertyName("isVatExempt")] public bool IsVatExempt { get; set; }
    [JsonPropertyName("requiresCustomerId")] public bool RequiresCustomerId { get; set; }
    [JsonPropertyName("idLabel")] public string? IdLabel { get; set; }
    [JsonPropertyName("category")] public int Category { get; set; }
}
