using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Mercurius.Shared.Bir
{
    // The printed documents a BIR-accredited POS produces (invoice, X reading, Z reading), laid out
    // as fixed-width text for a thermal receipt printer. Like BirTax.cs, Mercurius.Mobile compiles
    // this file in directly, so it stays dependency-free.

    /// <summary>The selling branch, as registered with BIR (its Certificate of Registration).</summary>
    public sealed class BirSellerInfo
    {
        public string? RegisteredName { get; init; }
        // Trade/business name, printed first when it differs from the registered name.
        public string? BusinessName { get; init; }
        public string? Address { get; init; }
        public string? Tin { get; init; }
        public string? BranchCode { get; init; }
        public bool IsVatRegistered { get; init; } = true;

        public bool IsComplete => !string.IsNullOrWhiteSpace(RegisteredName) && !string.IsNullOrWhiteSpace(Tin);

        // "123-456-789-00000"
        public string FormattedTin => BirFormat.Tin(Tin, BranchCode);
    }

    /// <summary>One POS machine, as registered with the branch's RDO.</summary>
    public sealed class BirTerminalInfo
    {
        public string? TerminalCode { get; init; }
        public string? TerminalName { get; init; }
        // Machine Identification Number, from the Permit to Use.
        public string? Min { get; init; }
        public string? SerialNumber { get; init; }
        public string? PtuNumber { get; init; }
        public DateTime? PtuDateIssued { get; init; }

        public bool IsRegistered => !string.IsNullOrWhiteSpace(Min) && !string.IsNullOrWhiteSpace(PtuNumber);
    }

    /// <summary>The POS software's own accreditation (QosmicRetail as supplier), printed in every receipt's footer.</summary>
    public sealed class BirSupplierInfo
    {
        public string? Name { get; init; }
        public string? Address { get; init; }
        public string? Tin { get; init; }
        public string? AccreditationNumber { get; init; }
        public DateTime? AccreditationDateIssued { get; init; }
        public DateTime? AccreditationValidUntil { get; init; }
    }

    public sealed class BirReceiptLine
    {
        public string Name { get; init; } = "";
        public decimal Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public VatType VatType { get; init; }
    }

    public sealed class BirReceipt
    {
        public BirSellerInfo Seller { get; init; } = new();
        public BirTerminalInfo Terminal { get; init; } = new();
        public BirSupplierInfo Supplier { get; init; } = new();
        public string InvoiceNumber { get; init; } = "";
        public DateTime IssuedAtLocal { get; init; }
        public string? CashierName { get; init; }
        public IReadOnlyList<BirReceiptLine> Lines { get; init; } = Array.Empty<BirReceiptLine>();
        public TaxSummary Summary { get; init; } = new();
        public string? PaymentMethod { get; init; }
        public decimal AmountPaid { get; init; }
        public decimal Change { get; init; }

        // The discount applied to the whole ticket, if any (Summary carries its amounts).
        public DiscountRule? Discount { get; init; }
        public string? DiscountIdNumber { get; init; }
        public string? DiscountHolderName { get; init; }
        public string? DiscountHolderTin { get; init; }

        public string? BuyerName { get; init; }
        public string? BuyerTin { get; init; }
        public string? BuyerAddress { get; init; }
        public string? BuyerBusinessStyle { get; init; }

        public bool IsReprint { get; init; }
        public DateTime? ReprintedAtLocal { get; init; }
    }

    /// <summary>The figures one sale contributes to an X or Z reading.</summary>
    public sealed class BirSaleTotals
    {
        public long InvoiceSequence { get; init; }
        public decimal Gross { get; init; }
        public decimal LessVat { get; init; }
        public decimal Discount { get; init; }
        public SpecialDiscountType DiscountType { get; init; }
        public decimal AmountDue { get; init; }
        public decimal VatableSales { get; init; }
        public decimal VatAmount { get; init; }
        public decimal VatExemptSales { get; init; }
        public decimal ZeroRatedSales { get; init; }
        public string? PaymentMethod { get; init; }
    }

    public enum BirReadingKind { X, Z }

    public sealed class BirReading
    {
        public BirReadingKind Kind { get; init; }
        public BirSellerInfo Seller { get; init; } = new();
        public BirTerminalInfo Terminal { get; init; } = new();
        // Z only: increments by one per Z reading on this terminal.
        public int ZCounter { get; init; }
        public int ResetCounter { get; init; }
        public DateTime ReadingAtLocal { get; init; }
        public string? CashierName { get; init; }

        public int SalesCount { get; init; }
        public long BeginningInvoice { get; init; }
        public long EndingInvoice { get; init; }
        public decimal Gross { get; init; }
        public decimal LessVat { get; init; }
        public decimal Discount { get; init; }
        public IReadOnlyDictionary<SpecialDiscountType, decimal> DiscountsByType { get; init; } = new Dictionary<SpecialDiscountType, decimal>();
        public decimal NetSales { get; init; }
        public decimal VatableSales { get; init; }
        public decimal VatAmount { get; init; }
        public decimal VatExemptSales { get; init; }
        public decimal ZeroRatedSales { get; init; }
        public decimal CashSales { get; init; }
        public decimal CardSales { get; init; }
        // The accumulating grand total: every sale this terminal has ever made. It only ever goes up.
        public decimal PreviousGrandTotal { get; init; }
        public decimal NewGrandTotal => PreviousGrandTotal + NetSales;

        /// <summary>Totals the given sales into a reading. Zero sales is a valid (empty) reading.</summary>
        public static BirReading Build(BirReadingKind kind, IEnumerable<BirSaleTotals> sales, BirSellerInfo seller, BirTerminalInfo terminal,
            int zCounter, int resetCounter, decimal previousGrandTotal, DateTime readingAtLocal, string? cashierName)
        {
            var list = sales.OrderBy(s => s.InvoiceSequence).ToList();
            return new BirReading
            {
                Kind = kind,
                Seller = seller,
                Terminal = terminal,
                ZCounter = zCounter,
                ResetCounter = resetCounter,
                ReadingAtLocal = readingAtLocal,
                CashierName = cashierName,
                SalesCount = list.Count,
                BeginningInvoice = list.Count > 0 ? list[0].InvoiceSequence : 0,
                EndingInvoice = list.Count > 0 ? list[^1].InvoiceSequence : 0,
                Gross = list.Sum(s => s.Gross),
                LessVat = list.Sum(s => s.LessVat),
                Discount = list.Sum(s => s.Discount),
                DiscountsByType = list.Where(s => s.DiscountType != SpecialDiscountType.None && s.Discount != 0)
                    .GroupBy(s => s.DiscountType)
                    .ToDictionary(g => g.Key, g => g.Sum(s => s.Discount)),
                NetSales = list.Sum(s => s.AmountDue),
                VatableSales = list.Sum(s => s.VatableSales),
                VatAmount = list.Sum(s => s.VatAmount),
                VatExemptSales = list.Sum(s => s.VatExemptSales),
                ZeroRatedSales = list.Sum(s => s.ZeroRatedSales),
                CashSales = list.Where(s => !string.Equals(s.PaymentMethod, "Card", StringComparison.OrdinalIgnoreCase)).Sum(s => s.AmountDue),
                CardSales = list.Where(s => string.Equals(s.PaymentMethod, "Card", StringComparison.OrdinalIgnoreCase)).Sum(s => s.AmountDue),
                PreviousGrandTotal = previousGrandTotal
            };
        }
    }

    public static class BirFormat
    {
        // Invoice serial numbers: BIR asks for at least six digits.
        public const int InvoiceDigits = 8;

        public static string InvoiceNumber(long sequence) => sequence.ToString(new string('0', InvoiceDigits), CultureInfo.InvariantCulture);

        // Digits only, grouped 3-3-3, then the 5-digit branch code ("00000" = head office).
        public static string Tin(string? tin, string? branchCode)
        {
            var digits = new string((tin ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return "";
            var main = digits.Length >= 9 ? $"{digits[..3]}-{digits[3..6]}-{digits[6..9]}" : digits;
            var branch = new string((branchCode ?? "").Where(char.IsDigit).ToArray());
            if (branch.Length == 0 && digits.Length > 9) branch = digits[9..];
            return branch.Length > 0 ? $"{main}-{branch.PadLeft(5, '0')}" : main;
        }

        public static string Money(decimal amount) => amount.ToString("#,##0.00", CultureInfo.InvariantCulture);
        public static string Date(DateTime? date) => date?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) ?? "";
        public static string Timestamp(DateTime date) => date.ToString("MM/dd/yyyy hh:mm tt", CultureInfo.InvariantCulture);

        public static string VatCode(VatType type) => type switch
        {
            VatType.VatExempt => "E",
            VatType.ZeroRated => "Z",
            _ => "V"
        };
    }

    /// <summary>Lays out invoices and readings as plain text, <c>width</c> characters wide (32 for 58 mm paper, 42-48 for 80 mm).</summary>
    public static class BirDocumentFormatter
    {
        public const int DefaultWidth = 40;

        public static string FormatReceipt(BirReceipt r, int width = DefaultWidth)
        {
            var w = new TextLayout(width);
            WriteSellerHeader(w, r.Seller, r.Terminal);
            w.Rule();

            if (r.IsReprint)
            {
                w.Center("*** REPRINT ***");
                if (r.ReprintedAtLocal is DateTime at) w.Center(BirFormat.Timestamp(at));
            }
            if (!r.Seller.IsComplete || !r.Terminal.IsRegistered)
            {
                w.Center("NOT A VALID INVOICE");
                w.Center("(BIR registration pending)");
            }
            w.Center("INVOICE");
            w.Pair("Invoice No.", r.InvoiceNumber);
            w.Pair("Date", BirFormat.Timestamp(r.IssuedAtLocal));
            if (!string.IsNullOrWhiteSpace(r.CashierName)) w.Pair("Cashier", r.CashierName!);
            if (!string.IsNullOrWhiteSpace(r.Terminal.TerminalName)) w.Pair("Terminal", r.Terminal.TerminalName!);
            w.Rule();

            for (var i = 0; i < r.Lines.Count; i++)
            {
                var line = r.Lines[i];
                var gross = i < r.Summary.Lines.Count ? r.Summary.Lines[i].Gross : BirTaxCalculator.Round(line.Quantity * line.UnitPrice);
                w.Wrap(line.Name);
                w.Pair($"  {line.Quantity.ToString("0.##", CultureInfo.InvariantCulture)} x {BirFormat.Money(line.UnitPrice)}",
                    $"{BirFormat.Money(gross)} {BirFormat.VatCode(r.Summary.Lines.Count > i ? r.Summary.Lines[i].VatType : line.VatType)}");
            }
            w.Rule();

            var itemCount = r.Lines.Sum(l => l.Quantity);
            w.Pair($"Total ({itemCount.ToString("0.##", CultureInfo.InvariantCulture)} item{(itemCount == 1 ? "" : "s")})", BirFormat.Money(r.Summary.Gross));
            var rule = r.Discount;
            if (r.Summary.LessVat != 0) w.Pair("Less: VAT 12%", BirFormat.Money(r.Summary.LessVat));
            if (r.Summary.Discount != 0) w.Pair(rule != null ? $"Less: {rule.ReceiptLabel}" : "Less: Discount", BirFormat.Money(r.Summary.Discount));
            w.Pair("AMOUNT DUE", BirFormat.Money(r.Summary.AmountDue));
            if (!string.IsNullOrWhiteSpace(r.PaymentMethod)) w.Pair(r.PaymentMethod!, BirFormat.Money(r.AmountPaid));
            if (r.Change != 0) w.Pair("Change", BirFormat.Money(r.Change));
            w.Rule();

            if (r.Seller.IsVatRegistered)
            {
                w.Pair("VATable Sales", BirFormat.Money(r.Summary.VatableSales));
                w.Pair("VAT Amount (12%)", BirFormat.Money(r.Summary.VatAmount));
                w.Pair("VAT-Exempt Sales", BirFormat.Money(r.Summary.VatExemptSales));
                w.Pair("Zero-Rated Sales", BirFormat.Money(r.Summary.ZeroRatedSales));
            }
            else
            {
                w.Pair("VAT-Exempt Sales", BirFormat.Money(r.Summary.VatExemptSales + r.Summary.VatableSales));
            }
            w.Rule();

            if (rule != null && rule.RequiresCustomerId)
            {
                w.Line($"{rule.Name} Discount");
                w.Line($"{rule.IdPrompt}: {r.DiscountIdNumber}");
                w.Line($"Name: {r.DiscountHolderName}");
                if (rule.AsksForTin) w.Line($"TIN: {r.DiscountHolderTin}");
                w.Line("Signature: " + new string('_', Math.Max(8, width - 11)));
                w.Rule();
            }

            // Left blank to fill in by hand when the cashier didn't capture the buyer (BIR's format
            // keeps these fields on every invoice so a VAT-registered buyer can claim input tax).
            w.Line("Sold to: " + Blank(r.BuyerName, width - 9));
            w.Line("Address: " + Blank(r.BuyerAddress, width - 9));
            w.Line("TIN: " + Blank(r.BuyerTin, width - 5));
            w.Line("Bus. Style: " + Blank(r.BuyerBusinessStyle, width - 12));
            w.Rule();

            WriteSupplierFooter(w, r.Supplier, r.Terminal);
            if (r.IsReprint) w.Center("THIS DOCUMENT IS NOT VALID FOR CLAIM OF INPUT TAX");
            return w.ToString();
        }

        /// <summary>
        /// A reprint of an already-issued invoice: the original text as it was printed (the
        /// e-journal copy), marked as a reprint and as not valid for input tax.
        /// </summary>
        public static string MarkAsReprint(string originalText, DateTime reprintedAtLocal, int width = DefaultWidth)
        {
            var w = new TextLayout(width);
            w.Center("*** REPRINT ***");
            w.Center(BirFormat.Timestamp(reprintedAtLocal));
            w.Rule();
            var body = originalText.TrimEnd('\n');
            var footer = new TextLayout(width);
            footer.Rule();
            footer.Center("*** REPRINT ***");
            footer.Center("THIS DOCUMENT IS NOT VALID FOR CLAIM OF INPUT TAX");
            return w + body + "\n" + footer;
        }

        private static readonly SpecialDiscountType[] StatutoryCategories =
        {
            SpecialDiscountType.SeniorCitizen, SpecialDiscountType.Pwd, SpecialDiscountType.SoloParent,
            SpecialDiscountType.NationalAthlete, SpecialDiscountType.MedalOfValor
        };

        public static string FormatReading(BirReading r, int width = DefaultWidth)
        {
            var w = new TextLayout(width);
            WriteSellerHeader(w, r.Seller, r.Terminal);
            w.Rule();
            w.Center(r.Kind == BirReadingKind.Z ? "Z-READING" : "X-READING");
            w.Pair("Report date", BirFormat.Timestamp(r.ReadingAtLocal));
            if (r.Kind == BirReadingKind.Z) w.Pair("Z-Counter", r.ZCounter.ToString(CultureInfo.InvariantCulture));
            w.Pair("Reset counter", r.ResetCounter.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(r.Terminal.TerminalName)) w.Pair("Terminal", r.Terminal.TerminalName!);
            if (!string.IsNullOrWhiteSpace(r.CashierName)) w.Pair("Cashier", r.CashierName!);
            w.Rule();

            w.Pair("Beg. Invoice No.", r.SalesCount > 0 ? BirFormat.InvoiceNumber(r.BeginningInvoice) : "-");
            w.Pair("End. Invoice No.", r.SalesCount > 0 ? BirFormat.InvoiceNumber(r.EndingInvoice) : "-");
            w.Pair("Transactions", r.SalesCount.ToString(CultureInfo.InvariantCulture));
            w.Rule();

            w.Pair("Gross Sales", BirFormat.Money(r.Gross));
            w.Pair("Less: VAT (SC/PWD etc.)", BirFormat.Money(r.LessVat));
            foreach (var category in StatutoryCategories)
            {
                var amount = r.DiscountsByType.TryGetValue(category, out var a) ? a : 0m;
                w.Pair($"Less: {DefaultDiscounts.CategoryLabel(category)} Discount", BirFormat.Money(amount));
            }
            var otherDiscounts = r.Discount - r.DiscountsByType.Values.Sum();
            if (otherDiscounts != 0) w.Pair("Less: Other Discounts", BirFormat.Money(otherDiscounts));
            w.Pair("Less: Returns/Refunds", BirFormat.Money(0));
            w.Pair("Less: Voids", BirFormat.Money(0));
            w.Pair("NET SALES", BirFormat.Money(r.NetSales));
            w.Rule();

            w.Pair("VATable Sales", BirFormat.Money(r.VatableSales));
            w.Pair("VAT Amount (12%)", BirFormat.Money(r.VatAmount));
            w.Pair("VAT-Exempt Sales", BirFormat.Money(r.VatExemptSales));
            w.Pair("Zero-Rated Sales", BirFormat.Money(r.ZeroRatedSales));
            w.Rule();

            w.Pair("Cash", BirFormat.Money(r.CashSales));
            w.Pair("Card", BirFormat.Money(r.CardSales));
            w.Rule();

            w.Pair("Previous Grand Total", BirFormat.Money(r.PreviousGrandTotal));
            w.Pair("Sales This Reading", BirFormat.Money(r.NetSales));
            w.Pair(r.Kind == BirReadingKind.Z ? "New Grand Total" : "Running Grand Total", BirFormat.Money(r.NewGrandTotal));
            if (r.Kind == BirReadingKind.X)
            {
                w.Rule();
                w.Center("X-reading: totals since the last Z-reading.");
                w.Center("Nothing was reset.");
            }
            return w.ToString();
        }

        private static void WriteSellerHeader(TextLayout w, BirSellerInfo seller, BirTerminalInfo terminal)
        {
            var business = string.IsNullOrWhiteSpace(seller.BusinessName) ? seller.RegisteredName : seller.BusinessName;
            if (!string.IsNullOrWhiteSpace(business)) w.CenterWrap(business!);
            if (!string.IsNullOrWhiteSpace(seller.BusinessName) && !string.IsNullOrWhiteSpace(seller.RegisteredName)
                && !string.Equals(seller.BusinessName, seller.RegisteredName, StringComparison.OrdinalIgnoreCase))
            {
                w.CenterWrap($"Owned & operated by: {seller.RegisteredName}");
            }
            if (!string.IsNullOrWhiteSpace(seller.Address)) w.CenterWrap(seller.Address!);
            var tin = seller.FormattedTin;
            w.Center((seller.IsVatRegistered ? "VAT REG TIN: " : "NON-VAT REG TIN: ") + (tin.Length > 0 ? tin : "(not set)"));
            w.Center("MIN: " + (string.IsNullOrWhiteSpace(terminal.Min) ? "(not set)" : terminal.Min));
            if (!string.IsNullOrWhiteSpace(terminal.SerialNumber)) w.Center("S/N: " + terminal.SerialNumber);
        }

        private static void WriteSupplierFooter(TextLayout w, BirSupplierInfo supplier, BirTerminalInfo terminal)
        {
            if (!string.IsNullOrWhiteSpace(supplier.Name))
            {
                w.CenterWrap("POS Provider: " + supplier.Name);
                if (!string.IsNullOrWhiteSpace(supplier.Address)) w.CenterWrap(supplier.Address!);
                if (!string.IsNullOrWhiteSpace(supplier.Tin)) w.Center("VAT REG TIN: " + BirFormat.Tin(supplier.Tin, null));
            }
            if (!string.IsNullOrWhiteSpace(supplier.AccreditationNumber))
            {
                w.CenterWrap("Accred. No.: " + supplier.AccreditationNumber);
                w.Center($"Date Issued: {BirFormat.Date(supplier.AccreditationDateIssued)}");
                if (supplier.AccreditationValidUntil.HasValue) w.Center($"Valid Until: {BirFormat.Date(supplier.AccreditationValidUntil)}");
            }
            w.CenterWrap("PTU No.: " + (string.IsNullOrWhiteSpace(terminal.PtuNumber) ? "(not set)" : terminal.PtuNumber));
            if (terminal.PtuDateIssued.HasValue) w.Center($"Date Issued: {BirFormat.Date(terminal.PtuDateIssued)}");
        }

        private static string Blank(string? value, int width) =>
            string.IsNullOrWhiteSpace(value) ? new string('_', Math.Max(6, width)) : value!;

        private sealed class TextLayout
        {
            private readonly int _width;
            private readonly StringBuilder _sb = new();

            public TextLayout(int width) => _width = Math.Max(24, width);

            public void Line(string text)
            {
                foreach (var part in Split(text)) _sb.Append(part).Append('\n');
            }

            public void Wrap(string text) => Line(text);

            public void Center(string text)
            {
                foreach (var part in Split(text))
                {
                    var pad = Math.Max(0, (_width - part.Length) / 2);
                    _sb.Append(new string(' ', pad)).Append(part).Append('\n');
                }
            }

            public void CenterWrap(string text) => Center(text);

            // Label on the left, value flush right; the label is cut short rather than the amount.
            public void Pair(string label, string value)
            {
                var room = _width - value.Length - 1;
                if (room < 1)
                {
                    Line(label);
                    _sb.Append(value.PadLeft(_width)).Append('\n');
                    return;
                }
                if (label.Length > room) label = label[..room];
                _sb.Append(label.PadRight(room)).Append(' ').Append(value).Append('\n');
            }

            public void Rule() => _sb.Append(new string('-', _width)).Append('\n');

            // Word-wraps to the paper width, breaking long words if it must.
            private IEnumerable<string> Split(string text)
            {
                var line = new StringBuilder();
                foreach (var word in text.Split(' '))
                {
                    var w = word;
                    while (w.Length > _width)
                    {
                        if (line.Length > 0) { yield return line.ToString(); line.Clear(); }
                        yield return w[.._width];
                        w = w[_width..];
                    }
                    if (line.Length > 0 && line.Length + 1 + w.Length > _width) { yield return line.ToString(); line.Clear(); }
                    if (line.Length > 0) line.Append(' ');
                    line.Append(w);
                }
                if (line.Length > 0 || text.Length == 0) yield return line.ToString();
            }

            public override string ToString() => _sb.ToString();
        }
    }
}
