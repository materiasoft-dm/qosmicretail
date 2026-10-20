using System.Text;
using Mercurius.Mobile.Data;
using Mercurius.Mobile.Models;
using Mercurius.Shared.Bir;

namespace Mercurius.Mobile.Services;

// This device as a BIR point-of-sale terminal: keeps its registration in step with the server,
// issues invoice receipts, takes X- and Z-readings, and exports the e-journal. The tax maths and
// the printed layouts are Mercurius.Shared.Bir's (shared with the server); this class feeds them
// the terminal's own data.
public class BirService
{
    private readonly LocalDatabase _localDatabase;
    private readonly SyncApiService _syncApi;
    private readonly DeviceSettingsService _deviceSettings;
    private readonly SessionService _session;

    public BirService(LocalDatabase localDatabase, SyncApiService syncApi, DeviceSettingsService deviceSettings, SessionService session)
    {
        _localDatabase = localDatabase;
        _syncApi = syncApi;
        _deviceSettings = deviceSettings;
        _session = session;
    }

    public Task<LocalTerminal?> GetTerminalAsync() => _localDatabase.GetTerminalAsync();

    // ---------------------------------------------------------------- registration

    // Run on every sync pass: tells the server this terminal's name and branch, and takes back the
    // branch's BIR details, the MIN/PTU, and the server's copy of the counters.
    public async Task<LocalTerminal> RefreshTerminalAsync(CancellationToken ct = default)
    {
        var config = await _syncApi.RegisterTerminalAsync(new TerminalRegisterRequest
        {
            DeviceId = _deviceSettings.GetDeviceId(),
            Name = _deviceSettings.GetTerminalName(),
            LocationId = _deviceSettings.GetSelectedLocationId()
        }, ct);
        return await ApplyConfigAsync(config);
    }

    public async Task<(bool Saved, string? Error)> SaveRegistrationAsync(string? min, string? serialNumber, string? ptuNumber, DateTime? ptuDateIssued)
    {
        var terminal = await GetTerminalAsync();
        if (terminal is not { TerminalId: > 0 })
        {
            return (false, "This device isn't registered as a terminal yet. Connect to the internet and sync first.");
        }
        var (config, error) = await _syncApi.UpdateTerminalRegistrationAsync(new TerminalRegistrationUpdate
        {
            TerminalId = terminal.TerminalId,
            MachineIdentificationNumber = min,
            SerialNumber = serialNumber,
            PermitToUseNumber = ptuNumber,
            PermitToUseDateIssued = ptuDateIssued
        });
        if (config == null) return (false, error);
        await ApplyConfigAsync(config);
        return (true, null);
    }

    // ---------------------------------------------------------------- discount types

    // Run on every sync pass: the tenant's active discount types, for the Sell page.
    public async Task<int> RefreshDiscountTypesAsync(CancellationToken ct = default)
    {
        var types = await _syncApi.GetDiscountTypesAsync(ct);
        await _localDatabase.ReplaceDiscountTypesAsync(types.Select((d, i) => new LocalDiscountType
        {
            Id = d.Id,
            Name = d.Name,
            ShortLabel = d.ShortLabel,
            ValueKind = d.ValueKind,
            Value = d.Value,
            IsVatExempt = d.IsVatExempt,
            RequiresCustomerId = d.RequiresCustomerId,
            IdLabel = d.IdLabel,
            Category = d.Category,
            SortOrder = i
        }).ToList());
        return types.Count;
    }

    public async Task<IReadOnlyList<DiscountRule>> GetDiscountRulesAsync() =>
        (await _localDatabase.GetDiscountTypesAsync()).Select(d => d.ToRule()).ToList();

    private Task<LocalTerminal> ApplyConfigAsync(TerminalConfigDto config) =>
        _localDatabase.UpdateTerminalAsync(local =>
        {
            var t = local ?? new LocalTerminal();
            if (t.TerminalId != config.TerminalId)
            {
                // First registration, or this device now sells for another pharmacy's account
                // (a different terminal there): start from the server's counters.
                t.TerminalId = config.TerminalId;
                t.LastInvoiceSequence = config.LastInvoiceSequence;
                t.LastZCounter = config.LastZCounter;
                t.AccumulatedGrandTotal = config.AccumulatedGrandTotal;
                t.ResetCounter = config.ResetCounter;
                t.LastJournalHash = null;
            }
            else
            {
                // Same terminal: the device issues the numbers, so it's normally ahead. The
                // server only wins when it knows of later numbers (e.g. the app's data was lost).
                t.LastInvoiceSequence = Math.Max(t.LastInvoiceSequence, config.LastInvoiceSequence);
                if (config.LastZCounter > t.LastZCounter)
                {
                    t.LastZCounter = config.LastZCounter;
                    t.AccumulatedGrandTotal = config.AccumulatedGrandTotal;
                }
                t.ResetCounter = Math.Max(t.ResetCounter, config.ResetCounter);
            }

            t.TerminalCode = config.TerminalCode;
            t.Name = config.Name;
            t.Min = config.MachineIdentificationNumber;
            t.SerialNumber = config.SerialNumber;
            t.PtuNumber = config.PermitToUseNumber;
            t.PtuDateIssued = config.PermitToUseDateIssued;
            t.LocationId = config.LocationId;
            t.SellerRegisteredName = config.SellerRegisteredName;
            t.SellerBusinessName = config.SellerBusinessName;
            t.SellerAddress = config.SellerAddress;
            t.SellerTin = config.SellerTin;
            t.SellerBranchCode = config.SellerBranchCode;
            t.SellerIsVatRegistered = config.SellerIsVatRegistered;
            t.SupplierName = config.SupplierName;
            t.SupplierAddress = config.SupplierAddress;
            t.SupplierTin = config.SupplierTin;
            t.AccreditationNumber = config.AccreditationNumber;
            t.AccreditationDateIssued = config.AccreditationDateIssued;
            t.AccreditationValidUntil = config.AccreditationValidUntil;
            t.CanEditRegistration = config.CanEditRegistration;
            t.LastRefreshedUtc = DateTime.UtcNow;
            return t;
        });

    public static BirSellerInfo Seller(LocalTerminal? t) => new()
    {
        RegisteredName = t?.SellerRegisteredName,
        BusinessName = t?.SellerBusinessName,
        Address = t?.SellerAddress,
        Tin = t?.SellerTin,
        BranchCode = t?.SellerBranchCode,
        IsVatRegistered = t?.SellerIsVatRegistered ?? true
    };

    public static BirTerminalInfo TerminalInfo(LocalTerminal? t, string? fallbackName = null) => new()
    {
        TerminalCode = t?.TerminalCode,
        TerminalName = string.IsNullOrWhiteSpace(t?.Name) ? fallbackName : t!.Name,
        Min = t?.Min,
        SerialNumber = t?.SerialNumber,
        PtuNumber = t?.PtuNumber,
        PtuDateIssued = t?.PtuDateIssued
    };

    public static BirSupplierInfo Supplier(LocalTerminal? t) => new()
    {
        Name = t?.SupplierName,
        Address = t?.SupplierAddress,
        Tin = t?.SupplierTin,
        AccreditationNumber = t?.AccreditationNumber,
        AccreditationDateIssued = t?.AccreditationDateIssued,
        AccreditationValidUntil = t?.AccreditationValidUntil
    };

    // ---------------------------------------------------------------- invoices

    public static TaxSummary Compute(IEnumerable<CartLine> cart, LocalTerminal? terminal, DiscountRule? discount) =>
        BirTaxCalculator.Compute(
            cart.Select(c => new TaxLineInput { Quantity = c.Quantity, UnitPrice = c.Price, VatType = (VatType)c.VatType }),
            terminal?.SellerIsVatRegistered ?? true,
            discount);

    public int ReceiptWidth => _deviceSettings.GetReceiptWidth();

    // The invoice as printed. Called by LocalDatabase.SaveSaleAsync once the sale has its serial.
    public string RenderReceipt(LocalSale sale, IReadOnlyList<LocalSaleItem> items, LocalTerminal? terminal, DiscountRule? discount)
    {
        var lines = items.Select(i => new BirReceiptLine
        {
            Name = i.ProductName,
            Quantity = i.Quantity,
            UnitPrice = i.SalePrice,
            VatType = (VatType)i.VatType
        }).ToList();
        var summary = BirTaxCalculator.Compute(
            lines.Select(l => new TaxLineInput { Quantity = l.Quantity, UnitPrice = l.UnitPrice, VatType = l.VatType }),
            terminal?.SellerIsVatRegistered ?? true,
            discount);
        var isCash = !string.Equals(sale.PaymentMethod, "Card", StringComparison.OrdinalIgnoreCase);

        return BirDocumentFormatter.FormatReceipt(new BirReceipt
        {
            Seller = Seller(terminal),
            Terminal = TerminalInfo(terminal, sale.SourceDeviceName),
            Supplier = Supplier(terminal),
            InvoiceNumber = sale.InvoiceSequence > 0 ? BirFormat.InvoiceNumber(sale.InvoiceSequence) : "(unnumbered)",
            IssuedAtLocal = sale.InvoiceDate.ToLocalTime(),
            CashierName = sale.CashierName,
            Lines = lines,
            Summary = summary,
            PaymentMethod = sale.PaymentMethod,
            AmountPaid = sale.PaidAmount,
            Change = isCash ? Math.Max(0, sale.PaidAmount - summary.AmountDue) : 0,
            Discount = discount,
            DiscountIdNumber = sale.SpecialDiscountIdNumber,
            DiscountHolderName = sale.SpecialDiscountHolderName,
            DiscountHolderTin = sale.SpecialDiscountHolderTin,
            BuyerName = sale.BuyerName,
            BuyerTin = sale.BuyerTin,
            BuyerAddress = sale.BuyerAddress,
            BuyerBusinessStyle = sale.BuyerBusinessStyle
        }, ReceiptWidth);
    }

    public string RenderReprint(LocalSale sale) =>
        BirDocumentFormatter.MarkAsReprint(sale.ReceiptText ?? "", DateTime.Now, ReceiptWidth);

    // ---------------------------------------------------------------- X / Z readings

    private static BirSaleTotals Totals(LocalSale s) => new()
    {
        InvoiceSequence = s.InvoiceSequence,
        Gross = s.GrossAmount,
        LessVat = s.LessVat,
        Discount = s.DiscountAmount,
        DiscountType = (SpecialDiscountType)s.SpecialDiscountType,
        AmountDue = s.GrandTotal,
        VatableSales = s.VatableSales,
        VatAmount = s.VatAmount,
        VatExemptSales = s.VatExemptSales,
        ZeroRatedSales = s.ZeroRatedSales,
        PaymentMethod = s.PaymentMethod
    };

    // Everything since the last Z-reading. Changes nothing.
    public async Task<string?> BuildXReadingAsync()
    {
        var terminal = await GetTerminalAsync();
        if (terminal is not { TerminalId: > 0 }) return null;
        var sales = await _localDatabase.GetSalesAwaitingZReadingAsync(terminal.TerminalId);
        var reading = BirReading.Build(BirReadingKind.X, sales.Select(Totals), Seller(terminal), TerminalInfo(terminal),
            terminal.LastZCounter, terminal.ResetCounter, terminal.AccumulatedGrandTotal, DateTime.Now, await _session.GetFullNameAsync());
        return BirDocumentFormatter.FormatReading(reading, ReceiptWidth);
    }

    // Closes the day: totals every sale since the last Z-reading, adds them to the grand total
    // and moves the Z counter on. Null if this device isn't a registered terminal yet.
    public async Task<LocalZReading?> TakeZReadingAsync()
    {
        var cashier = await _session.GetFullNameAsync();
        var width = ReceiptWidth;
        return await _localDatabase.TakeZReadingAsync((terminal, sales) =>
        {
            var reading = BirReading.Build(BirReadingKind.Z, sales.Select(Totals), Seller(terminal), TerminalInfo(terminal),
                terminal.LastZCounter + 1, terminal.ResetCounter, terminal.AccumulatedGrandTotal, DateTime.Now, cashier);
            decimal ByType(SpecialDiscountType type) => reading.DiscountsByType.TryGetValue(type, out var amount) ? amount : 0m;
            return new LocalZReading
            {
                SyncId = Guid.NewGuid(),
                TerminalId = terminal.TerminalId,
                ZCounter = reading.ZCounter,
                ResetCounter = reading.ResetCounter,
                ReadingUtc = DateTime.UtcNow,
                SalesCount = reading.SalesCount,
                BeginningInvoiceSequence = reading.BeginningInvoice,
                EndingInvoiceSequence = reading.EndingInvoice,
                GrossSales = reading.Gross,
                LessVat = reading.LessVat,
                DiscountTotal = reading.Discount,
                SeniorCitizenDiscount = ByType(SpecialDiscountType.SeniorCitizen),
                PwdDiscount = ByType(SpecialDiscountType.Pwd),
                SoloParentDiscount = ByType(SpecialDiscountType.SoloParent),
                NationalAthleteDiscount = ByType(SpecialDiscountType.NationalAthlete),
                MedalOfValorDiscount = ByType(SpecialDiscountType.MedalOfValor),
                NetSales = reading.NetSales,
                VatableSales = reading.VatableSales,
                VatAmount = reading.VatAmount,
                VatExemptSales = reading.VatExemptSales,
                ZeroRatedSales = reading.ZeroRatedSales,
                CashSales = reading.CashSales,
                CardSales = reading.CardSales,
                PreviousGrandTotal = reading.PreviousGrandTotal,
                NewGrandTotal = reading.NewGrandTotal,
                ReportText = BirDocumentFormatter.FormatReading(reading, width),
                IsSynced = false
            };
        });
    }

    // Part of every sync pass (SyncCoordinator). Returns how many the server confirmed.
    public async Task<int> PushZReadingsAsync(CancellationToken ct = default, ICollection<string>? rejections = null)
    {
        var pending = await _localDatabase.GetUnsyncedZReadingsAsync();
        if (pending.Count == 0) return 0;

        var results = await _syncApi.PushZReadingsAsync(pending.Select(z => new ZReadingPushDto
        {
            SyncId = z.SyncId,
            PosTerminalId = z.TerminalId,
            ZCounter = z.ZCounter,
            ResetCounter = z.ResetCounter,
            ReadingDate = z.ReadingUtc,
            SalesCount = z.SalesCount,
            BeginningInvoiceSequence = z.BeginningInvoiceSequence,
            EndingInvoiceSequence = z.EndingInvoiceSequence,
            GrossSales = z.GrossSales,
            LessVat = z.LessVat,
            DiscountTotal = z.DiscountTotal,
            SeniorCitizenDiscount = z.SeniorCitizenDiscount,
            PwdDiscount = z.PwdDiscount,
            SoloParentDiscount = z.SoloParentDiscount,
            NationalAthleteDiscount = z.NationalAthleteDiscount,
            MedalOfValorDiscount = z.MedalOfValorDiscount,
            NetSales = z.NetSales,
            VatableSales = z.VatableSales,
            VatAmount = z.VatAmount,
            VatExemptSales = z.VatExemptSales,
            ZeroRatedSales = z.ZeroRatedSales,
            CashSales = z.CashSales,
            CardSales = z.CardSales,
            PreviousGrandTotal = z.PreviousGrandTotal,
            NewGrandTotal = z.NewGrandTotal,
            ReportText = z.ReportText
        }).ToList(), ct);

        var confirmed = 0;
        foreach (var result in results)
        {
            if (string.IsNullOrEmpty(result.Error))
            {
                await _localDatabase.MarkZReadingSyncedAsync(result.SyncId);
                confirmed++;
            }
            else if (rejections != null && !rejections.Contains(result.Error))
            {
                rejections.Add(result.Error);
            }
        }
        return confirmed;
    }

    // ---------------------------------------------------------------- e-journal

    // Writes every invoice this terminal still holds, in serial order with each one's chain hash,
    // to a text file and returns its path (for the share sheet). Null when there's nothing to export.
    public async Task<string?> ExportJournalAsync()
    {
        var terminal = await GetTerminalAsync();
        if (terminal is not { TerminalId: > 0 }) return null;
        var sales = await _localDatabase.GetJournalAsync(terminal.TerminalId);
        if (sales.Count == 0) return null;

        var sb = new StringBuilder();
        sb.AppendLine($"E-JOURNAL  Terminal {terminal.TerminalCode} ({terminal.Name})  MIN {terminal.Min}");
        sb.AppendLine($"Exported {DateTime.Now:yyyy-MM-dd HH:mm}  Invoices {BirFormat.InvoiceNumber(sales[0].InvoiceSequence)} to {BirFormat.InvoiceNumber(sales[^1].InvoiceSequence)}");
        sb.AppendLine();
        string? previousHash = null;
        foreach (var sale in sales)
        {
            // Re-check the chain while exporting: a mismatch means a stored invoice was altered.
            var expected = LocalDatabase.JournalHash(previousHash, sale.ReceiptText!);
            var chainNote = previousHash == null || expected == sale.JournalHash ? "" : "  ** HASH MISMATCH **";
            sb.AppendLine($"===== Invoice {BirFormat.InvoiceNumber(sale.InvoiceSequence)}  {sale.InvoiceDate.ToLocalTime():yyyy-MM-dd HH:mm:ss}  hash {sale.JournalHash}{chainNote}");
            sb.AppendLine(sale.ReceiptText);
            previousHash = sale.JournalHash;
        }

        var path = Path.Combine(FileSystem.CacheDirectory, $"ejournal-{terminal.TerminalCode}-{DateTime.Now:yyyyMMdd-HHmm}.txt");
        await File.WriteAllTextAsync(path, sb.ToString());
        return path;
    }
}
