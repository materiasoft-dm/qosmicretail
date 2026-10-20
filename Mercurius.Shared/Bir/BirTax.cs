using System;
using System.Collections.Generic;
using System.Linq;

namespace Mercurius.Shared.Bir
{
    // BIR (Philippine Bureau of Internal Revenue) invoicing rules shared by the server and the
    // mobile POS. Mercurius.Mobile compiles this file in directly (a linked <Compile> item, not a
    // project reference), so it must stay dependency-free: plain C#, System.* only.

    /// <summary>How a product's sale is taxed. Stored as an int on Product, InvoiceItem and the mobile cache.</summary>
    public enum VatType
    {
        /// <summary>12% VAT, already included in the shelf price.</summary>
        Vatable = 0,
        /// <summary>No VAT, e.g. the maintenance medicines on the FDA's VAT-exempt list.</summary>
        VatExempt = 1,
        /// <summary>0% VAT (exports, some PEZA sales). Rare for a pharmacy.</summary>
        ZeroRated = 2
    }

    /// <summary>
    /// The BIR reporting category of a discount. X/Z readings must show each statutory discount on
    /// its own line; anything else (a store promo, an employee discount) is None and lands under
    /// "Other discounts". Stored as an int on DiscountType, Invoice and the mobile cache.
    /// </summary>
    public enum SpecialDiscountType
    {
        None = 0,
        SeniorCitizen = 1,
        Pwd = 2,
        SoloParent = 3,
        NationalAthlete = 4,
        MedalOfValor = 5
    }

    public enum DiscountValueKind
    {
        /// <summary>Value is a percentage, e.g. 20 for 20%.</summary>
        Percent = 0,
        /// <summary>Value is a peso amount taken off the ticket.</summary>
        FixedAmount = 1
    }

    /// <summary>
    /// One discount the cashier can apply to a ticket. The tenant defines these (Settings →
    /// Discount Types on the web); the mobile app caches them. The statutory ones are seeded as a
    /// starting point (<see cref="DefaultDiscounts"/>) but are ordinary, editable rows.
    /// </summary>
    public sealed class DiscountRule
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        // Printed on the receipt ("Less: SC Disc 20%"). Falls back to Name.
        public string? ShortLabel { get; init; }
        public DiscountValueKind ValueKind { get; init; }
        public decimal Value { get; init; }
        // The 12% VAT is taken off before the discount is computed, and the sale is reported as VAT-exempt.
        public bool IsVatExempt { get; init; }
        // Asks for the beneficiary's ID number and name, and prints them with a signature line.
        public bool RequiresCustomerId { get; init; }
        public string? IdLabel { get; init; }
        public SpecialDiscountType Category { get; init; }

        public string Code => string.IsNullOrWhiteSpace(ShortLabel) ? Name : ShortLabel!;
        public string IdPrompt => string.IsNullOrWhiteSpace(IdLabel) ? "ID No." : IdLabel!;
        // Asks for the holder's TIN too (optional on SC/PWD receipts).
        public bool AsksForTin => Category is SpecialDiscountType.SeniorCitizen or SpecialDiscountType.Pwd;

        /// <summary>"20%" or "₱50.00".</summary>
        public string ValueText => ValueKind == DiscountValueKind.Percent
            ? Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%"
            : "P" + Value.ToString("#,##0.00", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>What the receipt prints on the discount line, e.g. "SC Disc 20%".</summary>
        public string ReceiptLabel => $"{Code} Disc {ValueText}";
    }

    public static class DefaultDiscounts
    {
        // RA 9994 (seniors) and RA 10754 (PWD): 20% plus VAT exemption. RA 11861 (solo parents):
        // 10% plus VAT exemption on the child's goods. RA 10699 (national athletes and coaches):
        // 20% plus VAT exemption. RA 9049 (Medal of Valor): 20%; whether VAT is also exempt wasn't
        // confirmed, so it's seeded as a plain 20% discount until checked with a tax adviser.
        public static IReadOnlyList<DiscountRule> Statutory { get; } = new[]
        {
            new DiscountRule { Category = SpecialDiscountType.SeniorCitizen, Name = "Senior Citizen", ShortLabel = "SC", Value = 20m, IsVatExempt = true, RequiresCustomerId = true, IdLabel = "OSCA/SC ID No." },
            new DiscountRule { Category = SpecialDiscountType.Pwd, Name = "Person with Disability", ShortLabel = "PWD", Value = 20m, IsVatExempt = true, RequiresCustomerId = true, IdLabel = "PWD ID No." },
            new DiscountRule { Category = SpecialDiscountType.SoloParent, Name = "Solo Parent", ShortLabel = "SP", Value = 10m, IsVatExempt = true, RequiresCustomerId = true, IdLabel = "Solo Parent ID No." },
            new DiscountRule { Category = SpecialDiscountType.NationalAthlete, Name = "National Athlete/Coach", ShortLabel = "NAAC", Value = 20m, IsVatExempt = true, RequiresCustomerId = true, IdLabel = "PNSTM ID No." },
            new DiscountRule { Category = SpecialDiscountType.MedalOfValor, Name = "Medal of Valor", ShortLabel = "MOV", Value = 20m, IsVatExempt = false, RequiresCustomerId = true, IdLabel = "MOV ID No." },
        };

        public static DiscountRule? For(SpecialDiscountType category) =>
            Statutory.FirstOrDefault(r => r.Category == category);

        /// <summary>Short reading labels for the statutory categories, in report order.</summary>
        public static string CategoryLabel(SpecialDiscountType category) => category switch
        {
            SpecialDiscountType.SeniorCitizen => "SC",
            SpecialDiscountType.Pwd => "PWD",
            SpecialDiscountType.SoloParent => "Solo Parent",
            SpecialDiscountType.NationalAthlete => "NAAC",
            SpecialDiscountType.MedalOfValor => "MOV",
            _ => "Other"
        };
    }

    public sealed class TaxLineInput
    {
        public decimal Quantity { get; init; }
        // Shelf price. For a VATable line sold by a VAT-registered seller it already includes 12% VAT.
        public decimal UnitPrice { get; init; }
        public VatType VatType { get; init; }
    }

    public sealed class TaxLineResult
    {
        public VatType VatType { get; init; }
        // Quantity x price, before anything is taken off.
        public decimal Gross { get; init; }
        // VAT taken off because a special discount made the line VAT-exempt.
        public decimal LessVat { get; init; }
        public decimal Discount { get; init; }
        // What the customer pays for the line.
        public decimal Net { get; init; }
        // The line's contribution to the invoice's tax summary. Exempt and zero-rated amounts are
        // before the discount, which is shown on its own line (the usual BIR receipt layout).
        public decimal VatableSales { get; init; }
        public decimal VatAmount { get; init; }
        public decimal VatExemptSales { get; init; }
        public decimal ZeroRatedSales { get; init; }
    }

    public sealed class TaxSummary
    {
        public IReadOnlyList<TaxLineResult> Lines { get; init; } = Array.Empty<TaxLineResult>();
        public decimal Gross { get; init; }
        public decimal LessVat { get; init; }
        public decimal Discount { get; init; }
        public decimal AmountDue { get; init; }
        public decimal VatableSales { get; init; }
        public decimal VatAmount { get; init; }
        public decimal VatExemptSales { get; init; }
        public decimal ZeroRatedSales { get; init; }
    }

    /// <summary>
    /// Splits a sale into the amounts a BIR invoice has to show. Every amount is rounded to the
    /// centavo per line, and the totals are sums of the rounded lines, so a receipt always adds
    /// up: AmountDue = VatableSales + VatAmount + VatExemptSales + ZeroRatedSales - Discount.
    /// </summary>
    public static class BirTaxCalculator
    {
        public const decimal VatRate = 0.12m;

        /// <summary>Shorthand for the seeded statutory rule of a category (tests, and callers with no configured rule).</summary>
        public static TaxSummary Compute(IEnumerable<TaxLineInput> lines, bool sellerIsVatRegistered, SpecialDiscountType category) =>
            Compute(lines, sellerIsVatRegistered, DefaultDiscounts.For(category));

        public static TaxSummary Compute(IEnumerable<TaxLineInput> lines, bool sellerIsVatRegistered, DiscountRule? rule = null)
        {
            var inputs = lines.Select(l =>
            {
                var gross = Round(l.Quantity * l.UnitPrice);
                // A non-VAT seller charges no VAT on anything: every line is reported as VAT-exempt.
                var vatType = sellerIsVatRegistered ? l.VatType : VatType.VatExempt;
                var removesVat = rule != null && rule.IsVatExempt && vatType == VatType.Vatable;
                // What the discount is taken from: the VAT-free price when the rule removes VAT.
                var discountBase = removesVat ? Round(gross / (1 + VatRate)) : gross;
                return (Gross: gross, VatType: vatType, RemovesVat: removesVat, Base: discountBase);
            }).ToList();

            var discounts = AllocateDiscounts(inputs.Select(i => i.Base).ToList(), rule);
            var results = inputs.Select((i, n) => ComputeLine(i.Gross, i.VatType, i.RemovesVat, i.Base, discounts[n])).ToList();
            return new TaxSummary
            {
                Lines = results,
                Gross = results.Sum(r => r.Gross),
                LessVat = results.Sum(r => r.LessVat),
                Discount = results.Sum(r => r.Discount),
                AmountDue = results.Sum(r => r.Net),
                VatableSales = results.Sum(r => r.VatableSales),
                VatAmount = results.Sum(r => r.VatAmount),
                VatExemptSales = results.Sum(r => r.VatExemptSales),
                ZeroRatedSales = results.Sum(r => r.ZeroRatedSales)
            };
        }

        // Percent: each line's own share. Fixed amount: capped at the ticket's total, spread across
        // the lines in proportion to their price (the last line takes the rounding remainder), so
        // the VAT on every line is still computed on what that line actually sold for.
        private static IReadOnlyList<decimal> AllocateDiscounts(IReadOnlyList<decimal> bases, DiscountRule? rule)
        {
            var result = new decimal[bases.Count];
            if (rule == null || rule.Value <= 0 || bases.Count == 0) return result;

            if (rule.ValueKind == DiscountValueKind.Percent)
            {
                var rate = Math.Min(rule.Value, 100m) / 100m;
                for (var i = 0; i < bases.Count; i++) result[i] = Round(bases[i] * rate);
                return result;
            }

            var total = bases.Sum();
            if (total <= 0) return result;
            var amount = Math.Min(Round(rule.Value), total);
            var remaining = amount;
            for (var i = 0; i < bases.Count; i++)
            {
                var share = i == bases.Count - 1 ? remaining : Math.Min(Round(amount * bases[i] / total), remaining);
                share = Math.Min(share, bases[i]);
                result[i] = share;
                remaining -= share;
            }
            // Rounding can leave a centavo or two when the last line was too small to absorb it.
            for (var i = 0; remaining > 0 && i < bases.Count; i++)
            {
                var room = Math.Min(bases[i] - result[i], remaining);
                result[i] += room;
                remaining -= room;
            }
            return result;
        }

        private static TaxLineResult ComputeLine(decimal gross, VatType vatType, bool removesVat, decimal discountBase, decimal discount)
        {
            if (removesVat)
            {
                // Senior/PWD style: remove the VAT, then take the discount off the VAT-free price.
                return new TaxLineResult
                {
                    VatType = VatType.VatExempt,
                    Gross = gross,
                    LessVat = gross - discountBase,
                    Discount = discount,
                    Net = discountBase - discount,
                    VatExemptSales = discountBase
                };
            }

            if (vatType == VatType.Vatable)
            {
                // VAT stays, computed on the discounted price.
                var net = gross - discount;
                var vatable = Round(net / (1 + VatRate));
                return new TaxLineResult
                {
                    VatType = vatType,
                    Gross = gross,
                    Discount = discount,
                    Net = net,
                    // Shown before the discount, like the exempt/zero-rated columns, so the summary still adds up.
                    VatableSales = vatable + discount,
                    VatAmount = net - vatable
                };
            }

            return new TaxLineResult
            {
                VatType = vatType,
                Gross = gross,
                Discount = discount,
                Net = gross - discount,
                VatExemptSales = vatType == VatType.VatExempt ? gross : 0,
                ZeroRatedSales = vatType == VatType.ZeroRated ? gross : 0
            };
        }

        public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}
