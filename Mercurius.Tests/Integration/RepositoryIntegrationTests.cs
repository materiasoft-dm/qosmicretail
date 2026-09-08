using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Mercurius.Tests.Integration
{
    /// <summary>
    /// Integration tests for the Repository Pattern against a real Sqlite database (in-memory,
    /// kept alive for the test's lifetime via an open connection).
    /// These tests verify that the repository works correctly in a real database scenario.
    /// </summary>
    public class RepositoryIntegrationTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MercuriusDbContext _context;
        private readonly IUnitOfWork _unitOfWork;

        public RepositoryIntegrationTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<MercuriusDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new MercuriusDbContext(options);
            _context.Database.EnsureCreated();
            _unitOfWork = new EfUnitOfWork(_context);
        }

        public void Dispose()
        {
            _unitOfWork?.Dispose();
            _context.Dispose();
            _connection.Dispose();
        }

        [Fact]
        public async Task FullWorkflow_CreateReadUpdateDelete()
        {
            // Arrange
            var productRepo = _unitOfWork.Repository<Product>();

            // Create
            var product = new Product
            {
                Id = 1,
                Name = "Integration Test Product",
                ProductCode = "INT-001",
                CurrentStock = 100,
                IsActive = true
            };

            // Act - Create
            var createdProduct = await productRepo.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();

            // Assert - Create
            Assert.NotNull(createdProduct);
            Assert.Equal("Integration Test Product", createdProduct.Name);

            // Act - Read
            var retrievedProduct = await productRepo.GetByIdAsync(1);

            // Assert - Read
            Assert.NotNull(retrievedProduct);
            Assert.Equal(createdProduct.Name, retrievedProduct.Name);

            // Act - Update
            retrievedProduct.Name = "Updated Product Name";
            await productRepo.UpdateAsync(retrievedProduct);
            await _unitOfWork.SaveChangesAsync();

            // Assert - Update
            var updatedProduct = await productRepo.GetByIdAsync(1);
            Assert.NotNull(updatedProduct);
            Assert.Equal("Updated Product Name", updatedProduct.Name);

            // Act - Delete (Product has IsActive, so this soft-deletes rather than removing the row)
            await productRepo.DeleteAsync(1);
            await _unitOfWork.SaveChangesAsync();

            // Assert - Delete
            var deletedProduct = await productRepo.GetByIdAsync(1);
            Assert.NotNull(deletedProduct);
            Assert.False(deletedProduct.IsActive);
        }

        [Fact]
        public async Task ComplexQuery_FindWithPredicate()
        {
            // Arrange
            var productRepo = _unitOfWork.Repository<Product>();

            var products = new List<Product>
            {
                new Product { Id = 1, Name = "Active Product 1", ProductCode = "P001", IsActive = true, CurrentStock = 100 },
                new Product { Id = 2, Name = "Active Product 2", ProductCode = "P002", IsActive = true, CurrentStock = 50 },
                new Product { Id = 3, Name = "Inactive Product", ProductCode = "P003", IsActive = false, CurrentStock = 0 },
                new Product { Id = 4, Name = "Low Stock Product", ProductCode = "P004", IsActive = true, CurrentStock = 5 }
            };

            foreach (var p in products)
            {
                await productRepo.AddAsync(p);
            }
            await _unitOfWork.SaveChangesAsync();

            // Act - Find active products
            var activeProducts = await productRepo.FindAsync(p => p.IsActive);

            // Assert
            Assert.Equal(3, activeProducts.Count());

            // Act - Find low stock products
            var lowStockProducts = await productRepo.FindAsync(p => p.CurrentStock < 10);

            // Assert
            Assert.Equal(2, lowStockProducts.Count()); // Inactive Product (0) and Low Stock Product (5)
            Assert.Contains(lowStockProducts, p => p.Name == "Low Stock Product");
        }

        [Fact]
        public async Task Pagination_ShouldWorkCorrectly()
        {
            // Arrange
            var productRepo = _unitOfWork.Repository<Product>();

            // Add 25 products
            for (int i = 1; i <= 25; i++)
            {
                await productRepo.AddAsync(new Product
                {
                    Id = i,
                    Name = $"Product {i}",
                    ProductCode = $"P{i:D3}",
                    IsActive = true
                });
            }
            await _unitOfWork.SaveChangesAsync();

            // Act - Get page 1 (10 items)
            var (page1Items, totalCount1) = await productRepo.GetPagedAsync(null, null, 1, 10);

            // Assert
            Assert.Equal(10, page1Items.Count());
            Assert.Equal(25, totalCount1);

            // Act - Get page 3 (5 items remaining)
            var (page3Items, totalCount3) = await productRepo.GetPagedAsync(null, null, 3, 10);

            // Assert
            Assert.Equal(5, page3Items.Count());
            Assert.Equal(25, totalCount3);
        }

        [Fact]
        public async Task MultipleRepositories_ShouldWorkTogether()
        {
            // Arrange
            var productRepo = _unitOfWork.Repository<Product>();
            var customerRepo = _unitOfWork.Repository<Customer>();
            var invoiceRepo = _unitOfWork.Repository<Invoice>();
            var invoiceStatusRepo = _unitOfWork.Repository<InvoiceStatus>();

            // Create related data
            var product = new Product
            {
                Id = 1,
                Name = "Test Product",
                ProductCode = "TEST-001",
                IsActive = true
            };

            var customer = new Customer
            {
                Id = 1,
                FirstName = "John",
                LastName = "Doe",
                IsActive = true
            };

            // Invoice.StatusId is a required FK (Invoice.Status navigation) — SQLite enforces
            // it (unlike LiteDB), so a real InvoiceStatus row must exist first.
            var invoiceStatus = new InvoiceStatus
            {
                Id = 1,
                Name = "Draft"
            };

            var invoice = new Invoice
            {
                Id = 1,
                CustomerId = 1,
                InvoiceNumber = "INV-001",
                InvoiceDate = DateTime.Now,
                LocationId = 1,
                StatusId = 1
            };

            // Act
            await productRepo.AddAsync(product);
            await customerRepo.AddAsync(customer);
            await invoiceStatusRepo.AddAsync(invoiceStatus);
            await invoiceRepo.AddAsync(invoice);
            await _unitOfWork.SaveChangesAsync();

            // Assert
            Assert.Equal(1, await productRepo.CountAsync());
            Assert.Equal(1, await customerRepo.CountAsync());
            Assert.Equal(1, await invoiceRepo.CountAsync());
        }

        [Fact]
        public async Task BulkOperations_ShouldBeEfficient()
        {
            // Arrange
            var productRepo = _unitOfWork.Repository<Product>();
            var products = new List<Product>();

            for (int i = 1; i <= 100; i++)
            {
                products.Add(new Product
                {
                    Id = i,
                    Name = $"Bulk Product {i}",
                    ProductCode = $"BULK{i:D4}",
                    IsActive = true
                });
            }

            // Act
            var startTime = DateTime.Now;
            await productRepo.AddRangeAsync(products);
            await _unitOfWork.SaveChangesAsync();
            var endTime = DateTime.Now;

            // Assert
            var count = await productRepo.CountAsync();
            Assert.Equal(100, count);
            Assert.True((endTime - startTime).TotalSeconds < 5, "Bulk insert should complete in under 5 seconds");
        }

        // Note: a "concurrent writes on one shared repository instance" test existed here under
        // LiteDB (whose engine has its own internal thread safety). It doesn't port meaningfully
        // to EF Core: a single DbContext instance is explicitly not safe for concurrent use from
        // multiple threads — real concurrency safety in this app comes from ASP.NET Core giving
        // each request its own scoped DbContext, which this repository-level test can't exercise.

        // Note: an "EnsureIndex at runtime" test also existed here under LiteDB. Indexes are now
        // declared once in MercuriusDbContext.OnModelCreating rather than created imperatively,
        // so there's nothing runtime-callable left to test at the repository level.
    }
}