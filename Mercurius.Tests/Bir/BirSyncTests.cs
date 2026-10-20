using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Mercurius.Common;
using Mercurius.Common.Constants;
using Mercurius.Controllers.Api;
using Mercurius.Models.Sync;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Services;
using Mercurius.Shared.Bir;
using Mercurius.Shared.Sales;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mercurius.Tests.Bir
{
    /// <summary>
    /// The server side of BIR invoicing: terminal registration, invoices pushed with their BIR
    /// fields and per-terminal serials, Z-readings, and web checkout's VAT summary.
    /// </summary>
    public class BirSyncTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MercuriusDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly BatchPricingService _batchPricing = new();
        private static readonly Guid CashierId = Guid.NewGuid();

        private static readonly HashSet<string> SellerPermissions = new() { ModuleRegistry.Pages.NEWSALE_CREATE };
        private static readonly HashSet<string> AdminPermissions = new() { ModuleRegistry.Pages.NEWSALE_CREATE, ModuleRegistry.Pages.CONFIG_POS_TERMINALS };

        public BirSyncTests()
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

        private sealed class FixedPermissions : UserPermissions
        {
            private readonly HashSet<string> _granted;
            public FixedPermissions(HashSet<string> granted) : base(null!, null!) => _granted = granted;
            public override Task<HashSet<string>> GetAsync(string? userId) => Task.FromResult(new HashSet<string>(_granted));
        }

        private static readonly BirSupplierOptions Supplier = new()
        {
            Name = "Materiasoft",
            AccreditationNumber = "ACC-1",
            AccreditationDateIssued = new DateTime(2026, 9, 1)
        };

        private BirSyncController BirController(HashSet<string> permissions) =>
            WithUser(new BirSyncController(_unitOfWork, new FixedPermissions(permissions), Options.Create(Supplier)));

        private SyncController SyncController() =>
            WithUser(new SyncController(_unitOfWork, new SaleRecorder(_batchPricing), new FixedPermissions(SellerPermissions)));

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

        private async Task<Location> AddBranchAsync()
        {
            var address = new Address { Line1 = "123 Rizal St", Line2 = "", CityTown = "Quezon City", Province = "Metro Manila", Country = "PH", IsActive = true, CreatedDate = DateTime.UtcNow };
            var contact = new ContactInformation { MobilePhoneNumber = "", PhoneNumber = "", EmailAddress = "" };
            await _unitOfWork.Repository<Address>().AddAsync(address);
            await _unitOfWork.Repository<ContactInformation>().AddAsync(contact);
            await _unitOfWork.SaveChangesAsync();
            var location = new Location
            {
                Name = "Main Branch",
                AddressId = address.Id,
                ContactInformationId = contact.Id,
                TaxRegisteredName = "JUAN DELA CRUZ",
                TaxBusinessName = "Botika ni Juan",
                Tin = "123456789",
                TinBranchCode = "00000",
                IsVatRegistered = true
            };
            await _unitOfWork.Repository<Location>().AddAsync(location);
            await _unitOfWork.SaveChangesAsync();
            return location;
        }

        private async Task<Product> AddProductAsync(decimal stock, decimal price, VatType vatType = VatType.Vatable)
        {
            var product = new Product
            {
                Name = "Paracetamol 500mg",
                ProductCode = "PARA-" + Guid.NewGuid().ToString("N")[..6],
                CurrentStock = stock,
                CurrentSalePrice = price,
                CurrentCostPrice = 6m,
                IsActive = true,
                VatType = (int)vatType
            };
            await _unitOfWork.Repository<Product>().AddAsync(product);
            await _unitOfWork.SaveChangesAsync();
            return product;
        }

        private static T Value<T>(ActionResult<T> result) where T : class =>
            (result.Result as OkObjectResult)?.Value as T ?? throw new InvalidOperationException($"Expected 200 OK, got {result.Result?.GetType().Name}");

        private async Task<TerminalConfigDto> RegisterAsync(int locationId, string deviceId = "device-1") =>
            Value(await BirController(SellerPermissions).RegisterTerminal(new TerminalRegisterRequest { DeviceId = deviceId, Name = "Front Counter", LocationId = locationId }));

        [Fact]
        public async Task RegisterTerminal_IsIdempotentAndReturnsTheBranchTaxProfile()
        {
            var branch = await AddBranchAsync();

            var first = await RegisterAsync(branch.Id);
            var again = await RegisterAsync(branch.Id);

            Assert.Equal(first.TerminalId, again.TerminalId);
            Assert.Equal($"T{first.TerminalId}", first.TerminalCode);
            Assert.Equal("JUAN DELA CRUZ", first.SellerRegisteredName);
            Assert.Equal("Botika ni Juan", first.SellerBusinessName);
            Assert.Equal("123 Rizal St, Quezon City, Metro Manila", first.SellerAddress);
            Assert.Equal("ACC-1", first.AccreditationNumber);
            Assert.False(first.CanEditRegistration);
            Assert.Equal(1, await _context.PosTerminals.CountAsync());
        }

        [Fact]
        public async Task UpdateRegistration_NeedsThePosTerminalsPermission()
        {
            var branch = await AddBranchAsync();
            var terminal = await RegisterAsync(branch.Id);
            var update = new TerminalRegistrationUpdate
            {
                TerminalId = terminal.TerminalId,
                MachineIdentificationNumber = " 2610201234567 ",
                SerialNumber = "SN-1",
                PermitToUseNumber = "FP102026-123",
                PermitToUseDateIssued = new DateTime(2026, 10, 1)
            };

            var refused = await BirController(SellerPermissions).UpdateRegistration(update);
            Assert.Equal(403, Assert.IsType<ObjectResult>(refused.Result).StatusCode);

            var saved = Value(await BirController(AdminPermissions).UpdateRegistration(update));
            Assert.Equal("2610201234567", saved.MachineIdentificationNumber);
            Assert.Equal("FP102026-123", saved.PermitToUseNumber);
            Assert.True(saved.CanEditRegistration);
        }

        [Fact]
        public async Task PushInvoice_WithTerminalSerial_StoresTheBirInvoiceAsPrinted()
        {
            var branch = await AddBranchAsync();
            var terminal = await RegisterAsync(branch.Id);
            var product = await AddProductAsync(stock: 10, price: 112m);
            var senior = new DiscountType { Name = "Senior Citizen", ShortLabel = "SC", Value = 20m, IsVatExempt = true, RequiresCustomerId = true, Category = 1, IsActive = true };
            await _unitOfWork.Repository<DiscountType>().AddAsync(senior);
            await _unitOfWork.SaveChangesAsync();

            var push = new InvoicePushDto
            {
                SyncId = Guid.NewGuid(),
                InvoiceDate = DateTime.UtcNow,
                LocationId = branch.Id,
                PaymentMethod = "Cash",
                PaidAmount = 100m,
                PosTerminalId = terminal.TerminalId,
                TerminalInvoiceSequence = 7,
                VatExemptSales = 100m,
                LessVat = 12m,
                DiscountAmount = 20m,
                DiscountTypeId = senior.Id,
                DiscountName = "SC Disc 20%",
                SpecialDiscountType = (int)SpecialDiscountType.SeniorCitizen,
                SpecialDiscountIdNumber = "SC-1",
                SpecialDiscountHolderName = "Lola Basyang",
                ReceiptText = "INVOICE 00000007",
                JournalHash = new string('a', 64),
                Items = new List<InvoiceItemSyncDto>
                {
                    new()
                    {
                        SyncId = Guid.NewGuid(), ProductSyncId = product.SyncId, Quantity = 1, SalePrice = 112m,
                        VatType = (int)VatType.VatExempt, DiscountAmount = 20m, NetAmount = 80m
                    }
                }
            };

            var result = Assert.IsType<OkObjectResult>(await SyncController().PushInvoices(new List<InvoicePushDto> { push }));
            var item = Assert.Single((List<SyncPushResultItem>)result.Value!);
            Assert.Null(item.Error);

            var invoice = await _context.Invoices.Include(i => i.InvoiceItems).SingleAsync();
            Assert.Equal($"T{terminal.TerminalId}-00000007", invoice.InvoiceNumber);
            Assert.Equal(terminal.TerminalId, invoice.PosTerminalId);
            Assert.Equal(7, invoice.TerminalInvoiceSequence);
            Assert.Equal((int)SpecialDiscountType.SeniorCitizen, invoice.SpecialDiscountType);
            Assert.Equal(senior.Id, invoice.DiscountTypeId);
            Assert.Equal("SC Disc 20%", invoice.DiscountName);
            Assert.Equal(100m, invoice.VatExemptSales);
            Assert.Equal("INVOICE 00000007", invoice.ReceiptText);
            var line = Assert.Single(invoice.InvoiceItems);
            Assert.Equal(80m, line.CustomTotalPrice);
            Assert.Equal((int)VatType.VatExempt, line.VatType);
            Assert.Equal(7, (await _context.PosTerminals.SingleAsync()).LastInvoiceSequence);

            // The receipts pull hands the BIR fields back, so the till's copy keeps them.
            var page = Assert.IsType<ReceiptPullPage>(Assert.IsType<OkObjectResult>(await SyncController().PullReceipts(null, null)).Value);
            var receipt = Assert.Single(page.Items);
            Assert.Equal(7, receipt.TerminalInvoiceSequence);
            Assert.Equal("INVOICE 00000007", receipt.ReceiptText);
            Assert.Equal(80m, receipt.GrandTotal);
        }

        [Fact]
        public async Task PushInvoice_FromUnknownTerminal_IsRefusedAndStaysQueued()
        {
            var product = await AddProductAsync(stock: 10, price: 50m);
            var push = new InvoicePushDto
            {
                SyncId = Guid.NewGuid(),
                InvoiceDate = DateTime.UtcNow,
                PosTerminalId = 999,
                TerminalInvoiceSequence = 1,
                Items = new List<InvoiceItemSyncDto> { new() { SyncId = Guid.NewGuid(), ProductSyncId = product.SyncId, Quantity = 1, SalePrice = 50m } }
            };

            var result = Assert.IsType<OkObjectResult>(await SyncController().PushInvoices(new List<InvoicePushDto> { push }));

            Assert.NotNull(Assert.Single((List<SyncPushResultItem>)result.Value!).Error);
            Assert.Equal(0, await _context.Invoices.CountAsync());
        }

        [Fact]
        public async Task PushZReading_IsCreateOnceAndMovesTheTerminalCountersForward()
        {
            var branch = await AddBranchAsync();
            var terminal = await RegisterAsync(branch.Id);
            var reading = new ZReadingPushDto
            {
                SyncId = Guid.NewGuid(),
                PosTerminalId = terminal.TerminalId,
                ZCounter = 3,
                ReadingDate = DateTime.UtcNow,
                SalesCount = 2,
                BeginningInvoiceSequence = 11,
                EndingInvoiceSequence = 12,
                NetSales = 192m,
                PreviousGrandTotal = 1000m,
                NewGrandTotal = 1192m
            };

            var controller = BirController(SellerPermissions);
            await controller.PushZReadings(new List<ZReadingPushDto> { reading });
            var again = Assert.IsType<OkObjectResult>(await controller.PushZReadings(new List<ZReadingPushDto> { reading }));

            Assert.False(Assert.Single((List<SyncPushResultItem>)again.Value!).Created);
            Assert.Equal(1, await _context.PosZReadings.CountAsync());
            var saved = await _context.PosTerminals.AsNoTracking().SingleAsync();
            Assert.Equal(3, saved.LastZCounter);
            Assert.Equal(1192m, saved.AccumulatedGrandTotal);
            Assert.Equal(12, saved.LastInvoiceSequence);

            var reRegistered = await RegisterAsync(branch.Id);
            Assert.Equal(12, reRegistered.LastInvoiceSequence);
            Assert.Equal(1192m, reRegistered.AccumulatedGrandTotal);
        }

        [Fact]
        public async Task WebCheckout_RecordsTheVatSummaryFromEachProductsTaxClass()
        {
            var vatable = await AddProductAsync(stock: 10, price: 112m);
            var exempt = await AddProductAsync(stock: 10, price: 50m, VatType.VatExempt);
            var request = new CheckoutRequest
            {
                SyncId = Guid.NewGuid(),
                PaymentMethod = "Cash",
                AmountReceived = 500m,
                Items = new List<CartLineRequest>
                {
                    new() { ProductId = vatable.Id, Quantity = 1, UnitPrice = 112m },
                    new() { ProductId = exempt.Id, Quantity = 2, UnitPrice = 50m }
                }
            };

            var sales = WithUser(new SalesApiController(_unitOfWork, new SaleRecorder(_batchPricing)));
            Assert.IsType<OkObjectResult>(await sales.Checkout(request));

            var invoice = await _context.Invoices.Include(i => i.InvoiceItems).AsNoTracking().SingleAsync();
            Assert.Equal(100m, invoice.VatableSales);
            Assert.Equal(12m, invoice.VatAmount);
            Assert.Equal(100m, invoice.VatExemptSales);
            Assert.Contains(invoice.InvoiceItems, i => i.VatType == (int)VatType.VatExempt);
        }
    }
}
