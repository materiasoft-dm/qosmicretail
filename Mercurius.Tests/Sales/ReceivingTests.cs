using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Mercurius.Controllers.Api;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Services;
using Mercurius.Shared.Shipments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Mercurius.Tests.Sales
{
    /// <summary>
    /// Recording a shipment as Received is what puts stock on the shelf: stock, a batch at the
    /// delivery's prices, the shipment's lines, and the purchase order moving on.
    /// </summary>
    public class ReceivingTests : IDisposable
    {
        private const int Pending = 1, Received = 2, Damaged = 4;

        private readonly SqliteConnection _connection;
        private readonly MercuriusDbContext _context;
        private readonly IUnitOfWork _unitOfWork;

        public ReceivingTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _context = new MercuriusDbContext(new DbContextOptionsBuilder<MercuriusDbContext>().UseSqlite(_connection).Options, TestTenantContext.Instance);
            _context.Database.EnsureCreated();
            _unitOfWork = new EfUnitOfWork(_context, TestTenantContext.Instance);

            _context.ShipmentArrivalStatuses.AddRange(
                new ShipmentArrivalStatus { Id = Pending, Name = "Pending", IsActive = true, CssClass = "" },
                new ShipmentArrivalStatus { Id = Received, Name = "Received", IsActive = true, CssClass = "" },
                new ShipmentArrivalStatus { Id = 3, Name = "Delayed", IsActive = true, CssClass = "" },
                new ShipmentArrivalStatus { Id = Damaged, Name = "Damaged", IsActive = true, CssClass = "" });
            _context.SaveChanges();
        }

        public void Dispose()
        {
            _unitOfWork.Dispose();
            _context.Dispose();
            _connection.Dispose();
        }

        private ShipmentsApiController Controller()
        {
            var controller = new ShipmentsApiController(_unitOfWork, new StockReceiving(new BatchPricingService()));
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) }, "Test");
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
            return controller;
        }

        private async Task<Product> AddProductAsync(decimal stock, decimal price = 10m, decimal cost = 6m, string? name = null)
        {
            var product = new Product
            {
                Name = name ?? "Paracetamol 500mg", ProductCode = "P-" + Guid.NewGuid().ToString("N")[..6],
                CurrentStock = stock, CurrentSalePrice = price, CurrentCostPrice = cost, IsActive = true
            };
            await _unitOfWork.Repository<Product>().AddAsync(product);
            await _unitOfWork.SaveChangesAsync();
            return product;
        }

        private static ShipmentItemRequest Line(Product product, decimal quantity, decimal cost = 7m, decimal price = 12m) => new()
        {
            ProductId = product.Id, Quantity = quantity, UnitCost = cost, UnitSalePrice = price
        };

        private static CreateShipmentRequest Shipment(int statusId, params ShipmentItemRequest[] items) => new()
        {
            SyncId = Guid.NewGuid(), StatusId = statusId, Items = items.ToList()
        };

        private static string Error(IActionResult result) =>
            (string)Assert.IsType<BadRequestObjectResult>(result).Value!.GetType().GetProperty("error")!.GetValue(((BadRequestObjectResult)result).Value)!;

        private async Task<PurchaseOrder> AddPurchaseOrderAsync(string status, params (Product Product, decimal Quantity)[] lines)
        {
            var supplier = new Supplier { Name = "Norvic Drugs", IsActive = true };
            await _unitOfWork.Repository<Supplier>().AddAsync(supplier);
            await _unitOfWork.SaveChangesAsync();
            var order = new PurchaseOrder { OrderNumber = "PO-" + Guid.NewGuid().ToString("N")[..6], SupplierId = supplier.Id, Status = status };
            await _unitOfWork.Repository<PurchaseOrder>().AddAsync(order);
            await _unitOfWork.SaveChangesAsync();
            foreach (var (product, quantity) in lines)
            {
                await _unitOfWork.Repository<PurchaseOrderItem>().AddAsync(
                    new PurchaseOrderItem { PurchaseOrderId = order.Id, ProductId = product.Id, Quantity = quantity, EstimatedUnitCost = 7m });
            }
            await _unitOfWork.SaveChangesAsync();
            return order;
        }

        [Fact]
        public async Task ReceivedShipment_AddsStock_StartsABatchAtTheDeliveryPrices_AndKeepsTheLine()
        {
            var product = await AddProductAsync(stock: 5, price: 10m, cost: 6m);

            var request = Shipment(Received, Line(product, quantity: 20, cost: 7m, price: 12m));
            request.Items[0].BatchNumber = "LOT-42";
            request.Items[0].ExpiryDate = new DateTime(2028, 3, 31, 0, 0, 0, DateTimeKind.Utc);
            Assert.IsType<OkObjectResult>(await Controller().Create(request));

            _context.ChangeTracker.Clear();
            var saved = _context.Products.Single();
            Assert.Equal(25m, saved.CurrentStock);
            Assert.Equal(12m, saved.CurrentSalePrice);   // the new batch is the oldest with stock, so its price shows
            var batch = _context.MedicineBatches.Single();
            Assert.Equal(("LOT-42", 20m, 20m, 7m, 12m), (batch.BatchNumber, batch.InitialQuantity, batch.RemainingQuantity, batch.UnitCost, batch.UnitSalePrice));
            Assert.Equal(2028, batch.ExpiryDate.Year);
            var line = _context.ShipmentArrivalItems.Single();
            Assert.Equal((20m, 7m, 12m), (line.ItemCount, line.CostPrice, line.SalesPrice));
        }

        [Fact]
        public async Task ReceivedShipment_WithNoBatchNumberOrExpiry_GetsAGeneratedNumberAndNoExpiry()
        {
            var product = await AddProductAsync(stock: 0);

            Assert.IsType<OkObjectResult>(await Controller().Create(Shipment(Received, Line(product, 6))));

            _context.ChangeTracker.Clear();
            var batch = _context.MedicineBatches.Single();
            Assert.StartsWith("SHP-", batch.BatchNumber);
            Assert.Equal(MedicineBatch.NoExpiry, batch.ExpiryDate);
        }

        [Fact]
        public async Task ReceivedShipment_WithNothingOnIt_IsRefused()
        {
            var result = await Controller().Create(Shipment(Received));

            Assert.Contains("at least one item", Error(result));
            Assert.Empty(_context.ShipmentArrivals);
        }

        [Theory]
        [InlineData(Pending)]
        [InlineData(Damaged)]
        public async Task ShipmentThatHasNotArrivedAsGoods_CannotCarryItems(int statusId)
        {
            var product = await AddProductAsync(stock: 5);

            var result = await Controller().Create(Shipment(statusId, Line(product, 10)));

            Assert.Contains("Received", Error(result));
            Assert.Equal(5m, _context.Products.Single().CurrentStock);
            Assert.Empty(_context.ShipmentArrivals);
        }

        [Fact]
        public async Task PendingShipmentWithNoItems_IsJustRecorded()
        {
            Assert.IsType<OkObjectResult>(await Controller().Create(Shipment(Pending)));
            Assert.Single(_context.ShipmentArrivals);
            Assert.Empty(_context.MedicineBatches);
        }

        [Fact]
        public async Task SubmittingTheSameShipmentTwice_AddsTheStockOnce()
        {
            var product = await AddProductAsync(stock: 0);
            var request = Shipment(Received, Line(product, 10));

            var first = Assert.IsType<OkObjectResult>(await Controller().Create(request));
            var second = Assert.IsType<OkObjectResult>(await Controller().Create(request));

            _context.ChangeTracker.Clear();
            Assert.Equal(10m, _context.Products.Single().CurrentStock);
            Assert.Single(_context.ShipmentArrivals);
            Assert.Equal(first.Value!.GetType().GetProperty("id")!.GetValue(first.Value), second.Value!.GetType().GetProperty("id")!.GetValue(second.Value));
        }

        [Theory]
        [InlineData(0, "above zero")]
        [InlineData(-3, "above zero")]
        public async Task LineWithNoQuantity_IsRefusedAndNothingIsSaved(int quantity, string expected)
        {
            var product = await AddProductAsync(stock: 5);

            var result = await Controller().Create(Shipment(Received, Line(product, 4), Line(product, quantity)));

            Assert.Contains(expected, Error(result));
            Assert.Equal(5m, _context.Products.Single().CurrentStock);
            Assert.Empty(_context.MedicineBatches);
        }

        [Fact]
        public async Task LineForAProductThatIsGone_IsRefusedAndTheGoodLinesAreNotKept()
        {
            var product = await AddProductAsync(stock: 5);
            var gone = new ShipmentItemRequest { ProductId = 99999, Quantity = 3, UnitCost = 1, UnitSalePrice = 2 };

            var result = await Controller().Create(Shipment(Received, Line(product, 4), gone));

            Assert.Contains("catalog", Error(result));
            _context.ChangeTracker.Clear();
            Assert.Equal(5m, _context.Products.Single().CurrentStock);
            Assert.Empty(_context.ShipmentArrivals);   // rolled back with the rest
            Assert.Empty(_context.MedicineBatches);
        }

        [Fact]
        public async Task PartDelivery_LeavesThePurchaseOrderIncomplete_AndALaterOneCompletesIt()
        {
            var amoxicillin = await AddProductAsync(stock: 0, name: "Amoxicillin");
            var ibuprofen = await AddProductAsync(stock: 0, name: "Ibuprofen");
            var order = await AddPurchaseOrderAsync("OrderSent", (amoxicillin, 20m), (ibuprofen, 10m));

            // First delivery: all the amoxicillin, none of the ibuprofen.
            var first = Shipment(Received, Line(amoxicillin, 20));
            first.PurchaseOrderId = order.Id;
            Assert.IsType<OkObjectResult>(await Controller().Create(first));

            _context.ChangeTracker.Clear();
            Assert.Equal("ReceivedIncomplete", _context.PurchaseOrders.Single().Status);
            Assert.Equal(20m, _context.PurchaseOrderItems.Single(i => i.ProductId == amoxicillin.Id).ReceivedQuantity);

            // The order is still offered for receiving, and the rest completes it.
            var offered = Assert.IsType<OkObjectResult>(Controller().GetPurchaseOrders().Result);
            Assert.Single((List<PurchaseOrderOptionDto>)offered.Value!);

            var second = Shipment(Received, Line(ibuprofen, 10));
            second.PurchaseOrderId = order.Id;
            Assert.IsType<OkObjectResult>(await Controller().Create(second));

            _context.ChangeTracker.Clear();
            Assert.Equal("ReceivedComplete", _context.PurchaseOrders.Single().Status);
            Assert.Equal(10m, _context.PurchaseOrderItems.Single(i => i.ProductId == ibuprofen.Id).ReceivedQuantity);
            Assert.Empty(((List<PurchaseOrderOptionDto>)Assert.IsType<OkObjectResult>(Controller().GetPurchaseOrders().Result).Value!));
        }

        [Fact]
        public async Task PurchaseOrderNotApprovedYet_CannotBeReceivedAgainst()
        {
            var product = await AddProductAsync(stock: 0);
            var order = await AddPurchaseOrderAsync("PendingApproval", (product, 10m));
            var request = Shipment(Received, Line(product, 10));
            request.PurchaseOrderId = order.Id;

            var result = await Controller().Create(request);

            Assert.Contains("can't be received against", Error(result));
            Assert.Equal(0m, _context.Products.Single().CurrentStock);
        }

        [Fact]
        public async Task PurchaseOrderLines_ListWhatIsStillExpected()
        {
            var product = await AddProductAsync(stock: 0, price: 15m);
            var order = await AddPurchaseOrderAsync("Approved", (product, 30m));

            var lines = (List<ReceivableLineDto>)Assert.IsType<OkObjectResult>((await Controller().GetPurchaseOrderLines(order.Id)).Result).Value!;

            var line = Assert.Single(lines);
            Assert.Equal((30m, 0m, 7m, 15m), (line.Ordered, line.AlreadyReceived, line.UnitCost, line.UnitSalePrice));
        }
    }
}
