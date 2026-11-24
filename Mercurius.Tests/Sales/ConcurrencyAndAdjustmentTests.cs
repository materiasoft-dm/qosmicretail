using System;
using System.Linq;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Mercurius.Tests.Sales
{
    /// <summary>
    /// Two tills changing the same stock at the same moment must not lose an update (IVersioned), the
    /// retry helper must recover from that, and a write-off must take units out of the batches as
    /// well as the stock so FIFO pricing never offers units that are gone.
    /// </summary>
    public class ConcurrencyAndAdjustmentTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<MercuriusDbContext> _options;

        public ConcurrencyAndAdjustmentTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<MercuriusDbContext>().UseSqlite(_connection).Options;
            using var context = NewContext();
            context.Database.EnsureCreated();
        }

        public void Dispose() => _connection.Dispose();

        // Several contexts over one shared in-memory database: each stands for one request.
        private MercuriusDbContext NewContext() => new(_options, TestTenantContext.Instance);

        private int SeedProduct(decimal stock)
        {
            using var context = NewContext();
            var product = new Product { TenantId = TestTenantContext.DefaultTenantId, Name = "Amoxicillin", ProductCode = "AMOX-1", CurrentStock = stock, CurrentSalePrice = 10m, CurrentCostPrice = 6m, IsActive = true };
            context.Products.Add(product);
            context.SaveChanges();
            return product.Id;
        }

        [Fact]
        public async Task TwoRequestsSellingTheSameProduct_SecondOneIsRefusedInsteadOfOverwritingTheFirst()
        {
            var productId = SeedProduct(stock: 10);

            await using var till1 = NewContext();
            await using var till2 = NewContext();
            var seenByTill1 = await till1.Products.SingleAsync(p => p.Id == productId);
            var seenByTill2 = await till2.Products.SingleAsync(p => p.Id == productId);

            seenByTill1.CurrentStock -= 3;   // sells 3
            await till1.SaveChangesAsync();

            seenByTill2.CurrentStock -= 2;   // meanwhile sells 2, from the stale 10
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => till2.SaveChangesAsync());

            // Without the version check this ended at 8 (till 2 wrote 10 - 2) and till 1's sale vanished.
            await using var check = NewContext();
            Assert.Equal(7m, (await check.Products.SingleAsync(p => p.Id == productId)).CurrentStock);
        }

        [Fact]
        public async Task SequentialUpdatesInOneRequest_NeverConflictWithThemselves()
        {
            var productId = SeedProduct(stock: 10);

            await using var context = NewContext();
            var product = await context.Products.SingleAsync(p => p.Id == productId);
            for (var i = 0; i < 3; i++)
            {
                product.CurrentStock -= 1;
                await context.SaveChangesAsync();   // a sale's several saves, same tracked row
            }

            await using var check = NewContext();
            var saved = await check.Products.SingleAsync(p => p.Id == productId);
            Assert.Equal(7m, saved.CurrentStock);
            Assert.Equal(3, saved.Version);
        }

        [Fact]
        public async Task ConcurrencyRetry_RunsTheWorkAgainFromFreshState_AndKeepsOnlyTheSuccessfulAttempt()
        {
            var productId = SeedProduct(stock: 10);
            await using var context = NewContext();
            IUnitOfWork unitOfWork = new EfUnitOfWork(context, TestTenantContext.Instance);

            var attempts = 0;
            await ConcurrencyRetry.RunAsync(unitOfWork, async () =>
            {
                attempts++;
                var product = await unitOfWork.Repository<Product>().GetByIdAsync(productId);
                if (attempts == 1)
                {
                    // Another request sells 4 between this one reading the product and saving it.
                    await using var rival = NewContext();
                    var rivalCopy = await rival.Products.SingleAsync(p => p.Id == productId);
                    rivalCopy.CurrentStock -= 4;
                    await rival.SaveChangesAsync();
                }
                product!.CurrentStock -= 1;
                await unitOfWork.Repository<Product>().UpdateAsync(product);
                await unitOfWork.SaveChangesAsync();
                return true;
            });

            Assert.Equal(2, attempts);
            await using var check = NewContext();
            Assert.Equal(5m, (await check.Products.SingleAsync(p => p.Id == productId)).CurrentStock);   // 10 - 4 - 1
        }

        [Fact]
        public async Task ConcurrencyRetry_GivesUpAfterItsAttempts_SoACallerIsNeverStuck()
        {
            var productId = SeedProduct(stock: 10);
            await using var context = NewContext();
            IUnitOfWork unitOfWork = new EfUnitOfWork(context, TestTenantContext.Instance);

            var attempts = 0;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ConcurrencyRetry.RunAsync<bool>(unitOfWork, async () =>
            {
                attempts++;
                var product = await unitOfWork.Repository<Product>().GetByIdAsync(productId);
                await using var rival = NewContext();      // loses every time
                (await rival.Products.SingleAsync(p => p.Id == productId)).CurrentStock -= 1;
                await rival.SaveChangesAsync();
                product!.CurrentStock -= 1;
                await unitOfWork.Repository<Product>().UpdateAsync(product);
                await unitOfWork.SaveChangesAsync();
                return true;
            }));

            Assert.Equal(4, attempts);
        }

        [Fact]
        public async Task DiscardChanges_ForgetsAFailedSave_SoTheNextOneIsNotPoisoned()
        {
            var productId = SeedProduct(stock: 10);
            await using var context = NewContext();
            IUnitOfWork unitOfWork = new EfUnitOfWork(context, TestTenantContext.Instance);

            var stale = await unitOfWork.Repository<Product>().GetByIdAsync(productId);
            await using (var rival = NewContext())
            {
                (await rival.Products.SingleAsync(p => p.Id == productId)).CurrentStock -= 1;
                await rival.SaveChangesAsync();
            }
            stale!.CurrentStock = 0;
            await unitOfWork.Repository<Product>().UpdateAsync(stale);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => unitOfWork.SaveChangesAsync());

            unitOfWork.DiscardChanges();
            await unitOfWork.SaveChangesAsync();   // would re-throw for the stale product if it were still tracked

            await using var check = NewContext();
            Assert.Equal(9m, (await check.Products.SingleAsync(p => p.Id == productId)).CurrentStock);
        }

        // ---------------------------------------------------------------- write-offs and batches

        private async Task<(int ProductId, int BatchId)> SeedBatchedProductAsync(decimal remaining)
        {
            var productId = SeedProduct(stock: remaining);
            await using var context = NewContext();
            var batch = new MedicineBatch
            {
                TenantId = TestTenantContext.DefaultTenantId, ProductId = productId, BatchNumber = "B1", ExpiryDate = DateTime.UtcNow.AddMonths(2), ReceivedDate = DateTime.UtcNow.AddDays(-5),
                UnitCost = 6m, UnitSalePrice = 10m, InitialQuantity = remaining, RemainingQuantity = remaining, IsActive = true
            };
            context.MedicineBatches.Add(batch);
            context.AdjustmentReasons.AddRange(
                new AdjustmentReason { TenantId = TestTenantContext.DefaultTenantId, Name = "Expired", Description = "", IsActive = true, IsInbound = false },
                new AdjustmentReason { TenantId = TestTenantContext.DefaultTenantId, Name = "Found", Description = "", IsActive = true, IsInbound = true });
            await context.SaveChangesAsync();
            return (productId, batch.Id);
        }

        private async Task ApplyAsync(int productId, string reasonName, decimal quantity)
        {
            await using var context = NewContext();
            IUnitOfWork unitOfWork = new EfUnitOfWork(context, TestTenantContext.Instance);
            var reason = await context.AdjustmentReasons.SingleAsync(r => r.Name == reasonName);
            var adjustment = new Adjustment
            {
                ProductId = productId, ReasonId = reason.Id, Quantity = quantity, AdjustmentDate = DateTime.UtcNow,
                Note = "", LocationId = 0, CreatedDate = DateTime.UtcNow, IsActive = true
            };
            await unitOfWork.Repository<Adjustment>().AddAsync(adjustment);
            await unitOfWork.SaveChangesAsync();
            await new AdjustmentService(unitOfWork, new BatchPricingService()).ApplyAsync(adjustment);
        }

        [Fact]
        public async Task WriteOff_TakesTheUnitsOutOfTheBatchAsWellAsTheStock()
        {
            var (productId, batchId) = await SeedBatchedProductAsync(remaining: 10);

            await ApplyAsync(productId, "Expired", 4);

            await using var check = NewContext();
            Assert.Equal(6m, (await check.Products.SingleAsync(p => p.Id == productId)).CurrentStock);
            Assert.Equal(6m, (await check.MedicineBatches.SingleAsync(b => b.Id == batchId)).RemainingQuantity);
        }

        [Fact]
        public async Task InboundAdjustment_LeavesBatchesAlone()
        {
            var (productId, batchId) = await SeedBatchedProductAsync(remaining: 10);

            await ApplyAsync(productId, "Found", 3);

            await using var check = NewContext();
            Assert.Equal(13m, (await check.Products.SingleAsync(p => p.Id == productId)).CurrentStock);
            Assert.Equal(10m, (await check.MedicineBatches.SingleAsync(b => b.Id == batchId)).RemainingQuantity);
        }

        [Fact]
        public async Task WriteOff_OfAProductWithNoBatches_JustMovesStock()
        {
            var productId = SeedProduct(stock: 10);
            await using (var context = NewContext())
            {
                context.AdjustmentReasons.Add(new AdjustmentReason { TenantId = TestTenantContext.DefaultTenantId, Name = "Expired", Description = "", IsActive = true, IsInbound = false });
                await context.SaveChangesAsync();
            }

            await ApplyAsync(productId, "Expired", 4);

            await using var check = NewContext();
            Assert.Equal(6m, (await check.Products.SingleAsync(p => p.Id == productId)).CurrentStock);
        }
    }
}
