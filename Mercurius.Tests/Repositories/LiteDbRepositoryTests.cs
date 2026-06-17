using LiteDB;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Xunit;

namespace Mercurius.Tests.Repositories
{
    public class LiteDbRepositoryTests : IDisposable
    {
        private readonly LiteDatabase _database;
        private readonly LiteDbRepository<Product> _repository;

        public LiteDbRepositoryTests()
        {
            _database = new LiteDatabase(new MemoryStream());
            _repository = new LiteDbRepository<Product>(_database, "products");
        }

        public void Dispose()
        {
            _database?.Dispose();
        }

        [Fact]
        public async Task AddAsync_ShouldInsertProduct()
        {
            // Arrange
            var product = new Product
            {
                Id = 1,
                Name = "Test Product",
                PartCode = "TEST-001",
                CurrentStock = 100,
                IsActive = true
            };

            // Act
            var result = await _repository.AddAsync(product);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(product.Name, result.Name);
            Assert.Equal(product.PartCode, result.PartCode);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnProduct()
        {
            // Arrange
            var product = new Product
            {
                Id = 1,
                Name = "Test Product",
                PartCode = "TEST-001",
                CurrentStock = 100,
                IsActive = true
            };
            await _repository.AddAsync(product);

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
                new Product { Id = 1, Name = "Product 1", PartCode = "P001", IsActive = true },
                new Product { Id = 2, Name = "Product 2", PartCode = "P002", IsActive = true },
                new Product { Id = 3, Name = "Product 3", PartCode = "P003", IsActive = false }
            };

            foreach (var product in products)
            {
                await _repository.AddAsync(product);
            }

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
                new Product { Id = 1, Name = "Active Product", PartCode = "P001", IsActive = true },
                new Product { Id = 2, Name = "Inactive Product", PartCode = "P002", IsActive = false }
            };

            foreach (var product in products)
            {
                await _repository.AddAsync(product);
            }

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
                PartCode = "TEST-001",
                IsActive = true
            };
            await _repository.AddAsync(product);

            // Act
            product.Name = "Updated Name";
            await _repository.UpdateAsync(product);
            var result = await _repository.GetByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Updated Name", result.Name);
        }

        [Fact]
        public async Task DeleteAsync_ShouldRemoveProduct()
        {
            // Arrange
            var product = new Product
            {
                Id = 1,
                Name = "Test Product",
                PartCode = "TEST-001",
                IsActive = true
            };
            await _repository.AddAsync(product);

            // Act
            await _repository.DeleteAsync(1);
            var result = await _repository.GetByIdAsync(1);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ExistsAsync_ShouldReturnTrueForExistingProduct()
        {
            // Arrange
            var product = new Product
            {
                Id = 1,
                Name = "Test Product",
                PartCode = "TEST-001",
                IsActive = true
            };
            await _repository.AddAsync(product);

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
                new Product { Id = 1, Name = "Product 1", PartCode = "P001", IsActive = true },
                new Product { Id = 2, Name = "Product 2", PartCode = "P002", IsActive = true },
                new Product { Id = 3, Name = "Product 3", PartCode = "P003", IsActive = false }
            };

            foreach (var product in products)
            {
                await _repository.AddAsync(product);
            }

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
                new Product { Id = 1, Name = "Product 1", PartCode = "P001", IsActive = true },
                new Product { Id = 2, Name = "Product 2", PartCode = "P002", IsActive = true }
            };

            // Act
            var result = await _repository.AddRangeAsync(products);
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
                    PartCode = $"P{i:D3}",
                    IsActive = true
                });
            }

            // Act
            var (items, totalCount) = await _repository.GetPagedAsync(null, null, 1, 5);

            // Assert
            Assert.Equal(5, items.Count());
            Assert.Equal(10, totalCount);
        }
    }
}
