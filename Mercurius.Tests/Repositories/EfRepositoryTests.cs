using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Mercurius.Tests.Repositories
{
    // Backed by a real Sqlite connection (in-memory, kept open for the test's lifetime — the
    // in-memory database is destroyed as soon as the connection closes). Unlike LiteDB, nothing
    // is visible to a fresh query until SaveChangesAsync is called, so every mutation below is
    // followed by an explicit save.
    public class EfRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MercuriusDbContext _context;
        private readonly EfRepository<Product> _repository;

        public EfRepositoryTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<MercuriusDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new MercuriusDbContext(options);
            _context.Database.EnsureCreated();
            _repository = new EfRepository<Product>(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        [Fact]
        public async Task AddAsync_ShouldInsertProduct()
        {
            // Arrange
            var product = new Product
            {
                Id = 1,
                Name = "Test Product",
                ProductCode = "TEST-001",
                CurrentStock = 100,
                IsActive = true
            };

            // Act
            var result = await _repository.AddAsync(product);
            await _context.SaveChangesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(product.Name, result.Name);
            Assert.Equal(product.ProductCode, result.ProductCode);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnProduct()
        {
            // Arrange
            var product = new Product
            {
                Id = 1,
                Name = "Test Product",
                ProductCode = "TEST-001",
                CurrentStock = 100,
                IsActive = true
            };
            await _repository.AddAsync(product);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Product", result.Name);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNullForNonExistentProduct()
        {
            // Act
            var result = await _repository.GetByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnAllProducts()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product { Id = 1, Name = "Product 1", ProductCode = "P001", IsActive = true },
                new Product { Id = 2, Name = "Product 2", ProductCode = "P002", IsActive = true },
                new Product { Id = 3, Name = "Product 3", ProductCode = "P003", IsActive = false }
            };

            foreach (var product in products)
            {
                await _repository.AddAsync(product);
            }
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            Assert.Equal(3, result.Count());
        }

        [Fact]
        public async Task FindAsync_ShouldReturnFilteredProducts()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product { Id = 1, Name = "Active Product", ProductCode = "P001", IsActive = true },
                new Product { Id = 2, Name = "Inactive Product", ProductCode = "P002", IsActive = false }
            };

            foreach (var product in products)
            {
                await _repository.AddAsync(product);
            }
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.FindAsync(p => p.IsActive);

            // Assert
            Assert.Single(result);
            Assert.Equal("Active Product", result.First().Name);
        }

        [Fact]
        public async Task UpdateAsync_ShouldModifyProduct()
        {
            // Arrange
            var product = new Product
            {
                Id = 1,
                Name = "Original Name",
                ProductCode = "TEST-001",
                IsActive = true
            };
            await _repository.AddAsync(product);
            await _context.SaveChangesAsync();

            // Act
            product.Name = "Updated Name";
            await _repository.UpdateAsync(product);
            await _context.SaveChangesAsync();
            var result = await _repository.GetByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Updated Name", result.Name);
        }

        [Fact]
        public async Task DeleteAsync_ShouldSoftDeleteProduct()
        {
            // Arrange — Product has an IsActive flag, so nothing is ever hard-deleted for it.
            var product = new Product
            {
                Id = 1,
                Name = "Test Product",
                ProductCode = "TEST-001",
                IsActive = true
            };
            await _repository.AddAsync(product);
            await _context.SaveChangesAsync();

            // Act
            await _repository.DeleteAsync(1);
            await _context.SaveChangesAsync();
            var result = await _repository.GetByIdAsync(1);

            // Assert — the row still exists, just deactivated.
            Assert.NotNull(result);
            Assert.False(result.IsActive);
        }

        [Fact]
        public async Task ExistsAsync_ShouldReturnTrueForExistingProduct()
        {
            // Arrange
            var product = new Product
            {
                Id = 1,
                Name = "Test Product",
                ProductCode = "TEST-001",
                IsActive = true
            };
            await _repository.AddAsync(product);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.ExistsAsync(1);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ExistsAsync_ShouldReturnFalseForNonExistentProduct()
        {
            // Act
            var result = await _repository.ExistsAsync(999);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product { Id = 1, Name = "Product 1", ProductCode = "P001", IsActive = true },
                new Product { Id = 2, Name = "Product 2", ProductCode = "P002", IsActive = true },
                new Product { Id = 3, Name = "Product 3", ProductCode = "P003", IsActive = false }
            };

            foreach (var product in products)
            {
                await _repository.AddAsync(product);
            }
            await _context.SaveChangesAsync();

            // Act
            var totalCount = await _repository.CountAsync();
            var activeCount = await _repository.CountAsync(p => p.IsActive);

            // Assert
            Assert.Equal(3, totalCount);
            Assert.Equal(2, activeCount);
        }

        [Fact]
        public async Task AddRangeAsync_ShouldInsertMultipleProducts()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product { Id = 1, Name = "Product 1", ProductCode = "P001", IsActive = true },
                new Product { Id = 2, Name = "Product 2", ProductCode = "P002", IsActive = true }
            };

            // Act
            var result = await _repository.AddRangeAsync(products);
            await _context.SaveChangesAsync();
            var allProducts = await _repository.GetAllAsync();

            // Assert
            Assert.Equal(2, result.Count());
            Assert.Equal(2, allProducts.Count());
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnPagedResults()
        {
            // Arrange
            for (int i = 1; i <= 10; i++)
            {
                await _repository.AddAsync(new Product
                {
                    Id = i,
                    Name = $"Product {i}",
                    ProductCode = $"P{i:D3}",
                    IsActive = true
                });
            }
            await _context.SaveChangesAsync();

            // Act
            var (items, totalCount) = await _repository.GetPagedAsync(null, null, 1, 5);

            // Assert
            Assert.Equal(5, items.Count());
            Assert.Equal(10, totalCount);
        }
    }
}
