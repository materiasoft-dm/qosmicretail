using System;
using System.Linq;
using Mercurius.Shared.Bir;
using Xunit;

namespace Mercurius.Tests.Bir
{
    public class BirTaxCalculatorTests
    {
        private static TaxLineInput Line(decimal qty, decimal price, VatType type = VatType.Vatable) =>
            new() { Quantity = qty, UnitPrice = price, VatType = type };

        [Fact]
        public void VatableSale_SplitsVatOutOfTheShelfPrice()
        {
            var s = BirTaxCalculator.Compute(new[] { Line(1, 112m) }, sellerIsVatRegistered: true);

            Assert.Equal(112m, s.Gross);
            Assert.Equal(100m, s.VatableSales);
            Assert.Equal(12m, s.VatAmount);
            Assert.Equal(0m, s.VatExemptSales);
            Assert.Equal(112m, s.AmountDue);
        }

        [Fact]
        public void SeniorDiscountOnVatableItem_RemovesVatThenTakesTwentyPercent()
        {
            var s = BirTaxCalculator.Compute(new[] { Line(1, 112m) }, true, SpecialDiscountType.SeniorCitizen);

            Assert.Equal(12m, s.LessVat);
            Assert.Equal(20m, s.Discount);
            Assert.Equal(80m, s.AmountDue);
            // The sale becomes VAT-exempt; the exempt amount is shown before the discount.
            Assert.Equal(100m, s.VatExemptSales);
            Assert.Equal(0m, s.VatableSales);
            Assert.Equal(0m, s.VatAmount);
            Assert.Equal(VatType.VatExempt, s.Lines[0].VatType);
        }

        [Fact]
        public void PwdDiscountOnVatExemptItem_IsTwentyPercentOfThePrice()
        {
            var s = BirTaxCalculator.Compute(new[] { Line(2, 50m, VatType.VatExempt) }, true, SpecialDiscountType.Pwd);

            Assert.Equal(0m, s.LessVat);
            Assert.Equal(20m, s.Discount);
            Assert.Equal(80m, s.AmountDue);
            Assert.Equal(100m, s.VatExemptSales);
        }

        [Fact]
        public void SoloParentDiscount_IsTenPercentAndVatExempt()
        {
            var s = BirTaxCalculator.Compute(new[] { Line(1, 224m) }, true, SpecialDiscountType.SoloParent);

            Assert.Equal(24m, s.LessVat);
            Assert.Equal(20m, s.Discount);
            Assert.Equal(180m, s.AmountDue);
        }

        [Fact]
        public void MedalOfValorDiscount_KeepsVatOnTheDiscountedPrice()
        {
            var s = BirTaxCalculator.Compute(new[] { Line(1, 112m) }, true, SpecialDiscountType.MedalOfValor);

            Assert.Equal(0m, s.LessVat);
            Assert.Equal(22.40m, s.Discount);
            Assert.Equal(89.60m, s.AmountDue);
            Assert.Equal(9.60m, s.VatAmount);
        }

        private static DiscountRule Fixed(decimal amount, bool vatExempt = false) =>
            new() { Name = "Promo", ShortLabel = "PROMO", ValueKind = DiscountValueKind.FixedAmount, Value = amount, IsVatExempt = vatExempt };

        [Fact]
        public void FixedAmountDiscount_IsSpreadAcrossLinesByPrice()
        {
            var s = BirTaxCalculator.Compute(new[] { Line(1, 112m), Line(1, 56m, VatType.VatExempt) }, true, Fixed(30m));

            Assert.Equal(30m, s.Discount);
            Assert.Equal(138m, s.AmountDue);
            Assert.Equal(20m, s.Lines[0].Discount); // 112 of 168
            Assert.Equal(10m, s.Lines[1].Discount);
            // VAT on the vatable line is on what it sold for: 92 / 1.12.
            Assert.Equal(9.86m, s.VatAmount);
        }

        [Fact]
        public void FixedAmountDiscount_NeverTakesTheTicketBelowZero()
        {
            var s = BirTaxCalculator.Compute(new[] { Line(1, 40m), Line(2, 5m) }, true, Fixed(500m));

            Assert.Equal(50m, s.Discount);
            Assert.Equal(0m, s.AmountDue);
            Assert.All(s.Lines, l => Assert.True(l.Net >= 0));
        }

        [Fact]
        public void VatExemptFixedAmount_RemovesVatFirst()
        {
            var s = BirTaxCalculator.Compute(new[] { Line(1, 112m) }, true, Fixed(50m, vatExempt: true));

            Assert.Equal(12m, s.LessVat);
            Assert.Equal(50m, s.Discount);
            Assert.Equal(50m, s.AmountDue);
            Assert.Equal(100m, s.VatExemptSales);
        }

        [Fact]
        public void CustomPercentDiscount_UsesTheConfiguredValue()
        {
            var rule = new DiscountRule { Name = "Employee", ValueKind = DiscountValueKind.Percent, Value = 5m };
            var s = BirTaxCalculator.Compute(new[] { Line(1, 200m) }, true, rule);

            Assert.Equal(10m, s.Discount);
            Assert.Equal(190m, s.AmountDue);
            Assert.Equal("Employee Disc 5%", rule.ReceiptLabel);
        }

        [Fact]
        public void NonVatSeller_ReportsEverythingAsExemptWithNoVat()
        {
            var s = BirTaxCalculator.Compute(new[] { Line(1, 112m), Line(1, 30m, VatType.ZeroRated) }, sellerIsVatRegistered: false);

            Assert.Equal(0m, s.VatableSales);
            Assert.Equal(0m, s.VatAmount);
            Assert.Equal(142m, s.VatExemptSales);
            Assert.Equal(142m, s.AmountDue);
        }

        [Fact]
        public void MixedTicket_SummaryAlwaysAddsUpToTheAmountDue()
        {
            var random = new Random(20261020);
            foreach (var discount in Enum.GetValues<SpecialDiscountType>())
            {
                for (var run = 0; run < 200; run++)
                {
                    var lines = Enumerable.Range(0, random.Next(1, 8))
                        .Select(_ => Line(random.Next(1, 5) + (random.Next(0, 2) == 0 ? 0 : 0.5m),
                            Math.Round((decimal)random.NextDouble() * 900m + 1m, 2),
                            (VatType)random.Next(0, 3)))
                        .ToList();
                    var s = BirTaxCalculator.Compute(lines, sellerIsVatRegistered: random.Next(0, 4) != 0, discount);
                    AssertAddsUp(s);
                    var amount = Fixed(Math.Round((decimal)random.NextDouble() * 2000m, 2), vatExempt: random.Next(0, 2) == 0);
                    AssertAddsUp(BirTaxCalculator.Compute(lines, sellerIsVatRegistered: random.Next(0, 4) != 0, amount));
                }
            }
        }

        private static void AssertAddsUp(TaxSummary s)
        {
            Assert.True(s.AmountDue >= 0);
            Assert.Equal(s.AmountDue, s.VatableSales + s.VatAmount + s.VatExemptSales + s.ZeroRatedSales - s.Discount);
            Assert.Equal(s.AmountDue, s.Gross - s.LessVat - s.Discount);
            Assert.Equal(s.AmountDue, s.Lines.Sum(l => l.Net));
        }

        [Theory]
        [InlineData("123456789", "00000", "123-456-789-00000")]
        [InlineData("123-456-789", null, "123-456-789")]
        [InlineData("123-456-789-001", null, "123-456-789-00001")]
        [InlineData("", "00000", "")]
        public void Tin_IsFormattedWithItsBranchCode(string tin, string? branch, string expected)
        {
            Assert.Equal(expected, BirFormat.Tin(tin, branch));
        }
    }
}
