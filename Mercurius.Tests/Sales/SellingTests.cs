using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Controllers.Api;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Services;
using Mercurius.Shared.Invoices;
using Mercurius.Shared.Sales;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Mercurius.Tests.Sales
{
    /// <summary>
    /// Web checkout and refunds driven through the real controllers against an in-memory SQLite
    /// database: pricing, stock and batch bookkeeping, and what gets saved.
    /// </summary>
    public class SellingTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MercuriusDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly BatchPricingService _batchPricing = new();
        private static readonly Guid CashierId = Guid.NewGuid();

        public SellingTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<MercuriusDbContext>().UseSqlite(_connection).Options;
            _context = new MercuriusDbContext(options, TestTenantContext.Instance);
            _context.Database.EnsureCreated();
            _unitOfWork = new EfUnitOfWork(_context, TestTenantContext.Instance);

            foreach (var status in Enum.GetValues<StatusCollection.InvoiceStatus>())
            {
                _context.InvoiceStatuses.Add(new InvoiceStatus { Id = (int)status, Name = status.ToString(), CssClass = "" });
            }
            _context.SaveChanges();
        }

        public void Dispose()
        {
            _unitOfWork.Dispose();
            _context.Dispose();
            _connection.Dispose();
        }

        private SalesApiController SalesController() => WithUser(new SalesApiController(_unitOfWork, new SaleRecorder(_batchPricing)));

        private InvoicesApiController InvoicesController() =>
            WithUser(new InvoicesApiController(_unitOfWork, new AdjustmentService(_unitOfWork), _batchPricing));

        private static T WithUser<T>(T controller) where T : ControllerBase
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, CashierId.ToString()),
                new Claim(ClaimTypes.Name, "cashier@test")
            }, "Test");
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
            return controller;
        }

        private async Task<Product> AddProductAsync(decimal stock, decimal price = 10m, decimal cost = 6m)
        {
            var product = new Product
            {
                Name = "Paracetamol 500mg",
                ProductCode = "PARA-" + Guid.NewGuid().ToString("N")[..6],
                CurrentStock = stock,
                CurrentSalePrice = price,
                CurrentCostPrice = cost,
                IsActive = true
            };
            await _unitOfWork.Repository<Product>().AddAsync(product);
            await _unitOfWork.SaveChangesAsync();
            return product;
        }

        private async Task<MedicineBatch> AddBatchAsync(Product product, decimal remaining, decimal salePrice, decimal cost, int daysAgo)
        {
            var batch = new MedicineBatch
            {
                ProductId = product.Id,
                BatchNumber = "B" + daysAgo,
                ExpiryDate = DateTime.UtcNow.AddYears(1),
                ReceivedDate = DateTime.UtcNow.AddDays(-daysAgo),
                UnitCost = cost,
                UnitSalePrice = salePrice,
                InitialQuantity = remaining,
                RemainingQuantity = remaining,
                IsActive = true
            };
            await _unitOfWork.Repository<MedicineBatch>().AddAsync(batch);
            await _unitOfWork.SaveChangesAsync();
            return batch;
        }

        private static CheckoutRequest Ticket(Product product, int quantity, decimal? shownPrice, string method = "Cash", decimal received = 1000m) => new()
        {
            SyncId = Guid.NewGuid(),
            PaymentMethod = method,
            AmountReceived = received,
            Items = new List<CartLineRequest> { new() { ProductId = product.Id, Quantity = quantity, UnitPrice = shownPrice } }
        };

        [Fact]
        public async Task Checkout_TicketPriceOutOfDate_ReturnsCurrentPricesAndSavesNothing()
        {
            var product = await AddProductAsync(stock: 7, price: 10m);
            await AddBatchAsync(product, remaining: 3, salePrice: 10m, cost: 6m, daysAgo: 10);
            await AddBatchAsync(product, remaining: 4, salePrice: 12m, cost: 7m, daysAgo: 1);

            // The ticket shows the oldest batch's ₱10, but only the newer ₱12 batch can cover 4.
            var result = await SalesController().Checkout(Ticket(product, 4, shownPrice: 10m));

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            var body = Assert.IsType<CheckoutPriceChangeDto>(conflict.Value);
            Assert.Equal(12m, Assert.Single(body.Prices).UnitPrice);
            Assert.Empty(_context.Invoices);
            Assert.Equal(7m, _context.Products.Single().CurrentStock);
        }

        [Fact]
        public async Task Checkout_TicketPriceCurrent_ChargesItAndRecordsPaymentMethod()
        {
            var product = await AddProductAsync(stock: 10, price: 10m, cost: 6m);

            var request = Ticket(product, 2, shownPrice: 10m, method: "card");
            var result = Assert.IsType<OkObjectResult>(await SalesController().Checkout(request));

            var sale = Assert.IsType<CheckoutResultDto>(result.Value);
            Assert.Equal(20m, sale.Total);
            var invoice = _context.Invoices.Single();
            Assert.Equal(PaymentMethods.Card, invoice.PaymentMethod);
            Assert.EndsWith("-" + request.SyncId!.Value.ToString("N")[..4].ToUpperInvariant(), invoice.InvoiceNumber);
            var line = _context.InvoiceItems.Single();
            Assert.Equal(10m, line.SalePrice);
            Assert.Equal(6m, line.CostPrice);
            Assert.Equal(8m, _context.Products.Single().CurrentStock);
        }

        [Fact]
        public async Task Checkout_FromOlderTillWithoutTicketPrice_ChargesServerPrice()
        {
            var product = await AddProductAsync(stock: 10, price: 10m);

            var result = Assert.IsType<OkObjectResult>(await SalesController().Checkout(Ticket(product, 1, shownPrice: null)));

            Assert.Equal(10m, Assert.IsType<CheckoutResultDto>(result.Value).Total);
        }

        [Fact]
        public async Task OfflineSale_KeepsThePriceTheCustomerPaid()
        {
            var product = await AddProductAsync(stock: 10, price: 15m);
            var ticket = Ticket(product, 2, shownPrice: 12m);
            var offline = new OfflineSaleRequest
            {
                SyncId = ticket.SyncId,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-3),
                PaymentMethod = ticket.PaymentMethod,
                AmountReceived = ticket.AmountReceived,
                Items = ticket.Items
            };

            Assert.IsType<OkObjectResult>(await SalesController().SyncOfflineSale(offline));

            Assert.Equal(12m, _context.InvoiceItems.Single().SalePrice);
        }

        [Fact]
        public async Task Checkout_ProductNoLongerInCatalog_FailsInsteadOfDroppingTheLine()
        {
            var product = await AddProductAsync(stock: 10);
            var request = Ticket(product, 1, shownPrice: 10m);
            request.Items.Add(new CartLineRequest { ProductId = 9999, Quantity = 1, UnitPrice = 5m });

            Assert.IsType<BadRequestObjectResult>(await SalesController().Checkout(request));
            Assert.Empty(_context.Invoices);
        }

        [Fact]
        public async Task Checkout_NoSingleBatchCoversLine_TakesShortfallFromOtherBatchesWithoutGoingNegative()
        {
            var product = await AddProductAsync(stock: 7, price: 10m);
            var older = await AddBatchAsync(product, remaining: 4, salePrice: 10m, cost: 6m, daysAgo: 10);
            var newer = await AddBatchAsync(product, remaining: 3, salePrice: 12m, cost: 7m, daysAgo: 1);

            // 5 units: neither batch covers it alone, so it's priced from the newest (as before).
            Assert.IsType<OkObjectResult>(await SalesController().Checkout(Ticket(product, 5, shownPrice: 12m)));

            Assert.Equal(0m, _context.MedicineBatches.Single(b => b.Id == newer.Id).RemainingQuantity);
            Assert.Equal(2m, _context.MedicineBatches.Single(b => b.Id == older.Id).RemainingQuantity);
            Assert.Equal(2m, _context.Products.Single().CurrentStock);
        }

        [Fact]
        public async Task Checkout_Oversell_LeavesBatchesAtZeroAndLogsStockBeforeTheSale()
        {
            var product = await AddProductAsync(stock: 2, price: 10m);
            var batch = await AddBatchAsync(product, remaining: 2, salePrice: 10m, cost: 6m, daysAgo: 1);

            Assert.IsType<OkObjectResult>(await SalesController().Checkout(Ticket(product, 3, shownPrice: 10m)));

            Assert.Equal(0m, _context.MedicineBatches.Single(b => b.Id == batch.Id).RemainingQuantity);
            Assert.Equal(-1m, _context.Products.Single().CurrentStock);
            Assert.Equal(2m, _context.ZeroStockSaleAuditLogs.Single().StockAtTimeOfSale);
        }

        [Fact]
        public async Task Refund_WithRestock_PutsUnitsBackOnTheBatchTheyCameFrom()
        {
            var product = await AddProductAsync(stock: 5, price: 10m);
            var batch = await AddBatchAsync(product, remaining: 5, salePrice: 10m, cost: 6m, daysAgo: 1);
            Assert.IsType<OkObjectResult>(await SalesController().Checkout(Ticket(product, 3, shownPrice: 10m)));
            var reason = new RefundReason { Name = "Damaged", IsActive = true };
            await _unitOfWork.Repository<RefundReason>().AddAsync(reason);
            await _unitOfWork.SaveChangesAsync();

            var result = await InvoicesController().CreateRefund(new CreateRefundRequest
            {
                InvoiceItemId = _context.InvoiceItems.Single().Id,
                Quantity = 2,
                RefundReasonId = reason.Id,
                Restock = true
            });

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal(4m, _context.Products.Single().CurrentStock);
            Assert.Equal(4m, _context.MedicineBatches.Single(b => b.Id == batch.Id).RemainingQuantity);
        }

        [Theory]
        [InlineData("Card", null, "Card")]
        [InlineData(" cash ", null, "Cash")]
        [InlineData(null, "Paid by card", "Card")]
        [InlineData(null, "Senior (Paid by cash)", "Cash")]
        [InlineData(null, null, "Cash")]
        public void PaymentMethod_FromTillOrOlderNotes(string? method, string? notes, string expected)
        {
            Assert.Equal(expected, PaymentMethods.Normalize(method, notes));
        }
    }
}
