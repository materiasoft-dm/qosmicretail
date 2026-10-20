using System;
using System.Linq;
using Mercurius.Shared.Bir;
using Xunit;

namespace Mercurius.Tests.Bir
{
    public class BirDocumentFormatterTests
    {
        private static readonly BirSellerInfo Seller = new()
        {
            RegisteredName = "JUAN DELA CRUZ",
            BusinessName = "Botika ni Juan",
            Address = "123 Rizal St, Quezon City",
            Tin = "123456789",
            BranchCode = "00000",
            IsVatRegistered = true
        };

        private static readonly BirTerminalInfo Terminal = new()
        {
            TerminalCode = "T3",
            TerminalName = "Front Counter",
            Min = "2610201234567",
            SerialNumber = "SN-0001",
            PtuNumber = "FP102026-123-0000001-00000",
            PtuDateIssued = new DateTime(2026, 10, 1)
        };

        private static readonly BirSupplierInfo Supplier = new()
        {
            Name = "Materiasoft",
            Tin = "987654321000",
            AccreditationNumber = "0012345678901234567890",
            AccreditationDateIssued = new DateTime(2026, 9, 1),
            AccreditationValidUntil = new DateTime(2031, 9, 1)
        };

        private static BirReceipt Receipt(BirTerminalInfo terminal, SpecialDiscountType discount = SpecialDiscountType.None)
        {
            var lines = new[]
            {
                new BirReceiptLine { Name = "Paracetamol 500mg tablet, box of 100 (generic)", Quantity = 2, UnitPrice = 56m, VatType = VatType.Vatable },
                new BirReceiptLine { Name = "Metformin 500mg", Quantity = 1, UnitPrice = 100m, VatType = VatType.VatExempt }
            };
            return new BirReceipt
            {
                Seller = Seller,
                Terminal = terminal,
                Supplier = Supplier,
                InvoiceNumber = BirFormat.InvoiceNumber(42),
                IssuedAtLocal = new DateTime(2026, 10, 20, 9, 15, 0),
                CashierName = "Ana",
                Lines = lines,
                Summary = BirTaxCalculator.Compute(lines.Select(l => new TaxLineInput { Quantity = l.Quantity, UnitPrice = l.UnitPrice, VatType = l.VatType }), true, discount),
                PaymentMethod = "Cash",
                AmountPaid = 300m,
                Change = 88m,
                Discount = DefaultDiscounts.For(discount),
                DiscountIdNumber = discount == SpecialDiscountType.None ? null : "SC-998877",
                DiscountHolderName = discount == SpecialDiscountType.None ? null : "Lola Basyang"
            };
        }

        [Theory]
        [InlineData(32)]
        [InlineData(40)]
        [InlineData(48)]
        public void Receipt_ShowsTheBirFieldsAndFitsThePaper(int width)
        {
            var text = BirDocumentFormatter.FormatReceipt(Receipt(Terminal), width);

            Assert.All(text.Split('\n'), line => Assert.True(line.Length <= width, $"'{line}' is wider than {width}"));
            Assert.Contains("VAT REG TIN: 123-456-789-00000", text);
            Assert.Contains("MIN: 2610201234567", text);
            Assert.Contains("00000042", text);
            Assert.Contains("FP102026-123-0000001-00000", text);
            Assert.Contains("0012345678901234567890", text);
            Assert.Contains("212.00", text); // 2 x 56 + 100
            Assert.DoesNotContain("NOT A VALID INVOICE", text);
        }

        [Fact]
        public void Receipt_WithoutPermitToUse_SaysItIsNotAValidInvoice()
        {
            var text = BirDocumentFormatter.FormatReceipt(Receipt(new BirTerminalInfo { TerminalName = "Front Counter" }));

            Assert.Contains("NOT A VALID INVOICE", text);
            Assert.Contains("MIN: (not set)", text);
        }

        [Fact]
        public void SeniorReceipt_ShowsTheDiscountBlockWithSignatureLine()
        {
            var text = BirDocumentFormatter.FormatReceipt(Receipt(Terminal, SpecialDiscountType.SeniorCitizen));

            Assert.Contains("Less: VAT 12%", text);
            Assert.Contains("Less: SC Disc 20%", text);
            Assert.Contains("OSCA/SC ID No.: SC-998877", text);
            Assert.Contains("Name: Lola Basyang", text);
            Assert.Contains("Signature:", text);
        }

        [Fact]
        public void Reprint_IsMarkedAndNotValidForInputTax()
        {
            var original = Receipt(Terminal);
            var reprint = new BirReceipt
            {
                Seller = original.Seller, Terminal = original.Terminal, Supplier = original.Supplier,
                InvoiceNumber = original.InvoiceNumber, IssuedAtLocal = original.IssuedAtLocal, Lines = original.Lines,
                Summary = original.Summary, IsReprint = true, ReprintedAtLocal = new DateTime(2026, 10, 21, 8, 0, 0)
            };

            var text = BirDocumentFormatter.FormatReceipt(reprint);

            Assert.Contains("*** REPRINT ***", text);
            Assert.Contains("NOT VALID FOR CLAIM", text);
        }

        [Fact]
        public void MarkAsReprint_KeepsTheOriginalTextBetweenReprintBanners()
        {
            var original = BirDocumentFormatter.FormatReceipt(Receipt(Terminal));

            var text = BirDocumentFormatter.MarkAsReprint(original, new DateTime(2026, 10, 21, 8, 0, 0));

            Assert.StartsWith(new string(' ', (40 - 15) / 2) + "*** REPRINT ***", text);
            Assert.Contains(original.TrimEnd('\n'), text);
            Assert.Contains("NOT VALID FOR CLAIM", text[(text.Length - 120)..]);
        }

        [Fact]
        public void ZReading_TotalsTheSalesAndCarriesTheGrandTotalForward()
        {
            var sales = new[]
            {
                new BirSaleTotals { InvoiceSequence = 7, Gross = 112m, AmountDue = 112m, VatableSales = 100m, VatAmount = 12m, PaymentMethod = "Cash" },
                new BirSaleTotals { InvoiceSequence = 8, Gross = 112m, LessVat = 12m, Discount = 20m, DiscountType = SpecialDiscountType.SeniorCitizen,
                    AmountDue = 80m, VatExemptSales = 100m, PaymentMethod = "Card" }
            };

            var reading = BirReading.Build(BirReadingKind.Z, sales, Seller, Terminal, zCounter: 5, resetCounter: 0,
                previousGrandTotal: 1000m, readingAtLocal: new DateTime(2026, 10, 20, 21, 0, 0), cashierName: "Ana");
            var text = BirDocumentFormatter.FormatReading(reading);

            Assert.Equal(192m, reading.NetSales);
            Assert.Equal(1192m, reading.NewGrandTotal);
            Assert.Equal(7, reading.BeginningInvoice);
            Assert.Equal(8, reading.EndingInvoice);
            Assert.Equal(112m, reading.CashSales);
            Assert.Equal(80m, reading.CardSales);
            Assert.Equal(20m, reading.DiscountsByType[SpecialDiscountType.SeniorCitizen]);
            Assert.Contains("Z-READING", text);
            Assert.Contains("1,192.00", text);
            Assert.Contains("00000007", text);
        }
    }
}
