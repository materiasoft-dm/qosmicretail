using LiteDB;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using System;
using System.Threading.Tasks;
using System.IO;
using Xunit;

namespace Mercurius.Tests.Repositories
{
    public class LiteDbUnitOfWorkTests : IDisposable
    {
        private readonly LiteDatabase _database;
        private readonly LiteDbUnitOfWork _unitOfWork;

        public LiteDbUnitOfWorkTests()
        {
            _database = new LiteDatabase(new MemoryStream());
            _unitOfWork = new LiteDbUnitOfWork(_database);
        }

        public void Dispose()
        {
            _database?.Dispose();
        }

        [Fact]
        public void Repository_ShouldReturnRepositoryForType()
        {
            // Act
            var productRepository = _unitOfWork.Repository<Product>();
            var customerRepository = _unitOfWork.Repository<Customer>();

            // Assert
            Assert.NotNull(productRepository);
            Assert.NotNull(customerRepository);
            Assert.IsType<LiteDbRepository<Product>>(productRepository);
            Assert.IsType<LiteDbRepository<Customer>>(customerRepository);
        }

        [Fact]
        public void Repository_ShouldReturnSameInstanceForSameType()
        {
            // Act
            var repo1 = _unitOfWork.Repository<Product>();
            var repo2 = _unitOfWork.Repository<Product>();

            // Assert
            Assert.Same(repo1, repo2);
        }

        [Fact]
        public async Task SaveChangesAsync_ShouldCompleteWithoutException()
        {
            // Act — SaveChangesAsync returns Task (checkpoint only; LiteDB auto-saves)
            await _unitOfWork.SaveChangesAsync();

            // Assert — no exception means success
            Assert.True(true);
        }

        [Fact]
        public async Task BeginTransactionAsync_ShouldCompleteWithoutException()
        {
            // Act & Assert
            await _unitOfWork.BeginTransactionAsync();
        }

        [Fact]
        public async Task CommitTransactionAsync_ShouldCompleteWithoutException()
        {
            // Arrange
            await _unitOfWork.BeginTransactionAsync();

            // Act & Assert
            await _unitOfWork.CommitTransactionAsync();
        }

        [Fact]
        public async Task CompleteWorkflow_ShouldWorkWithMultipleRepositories()
        {
            // Arrange
            var productRepo = _unitOfWork.Repository<Product>();
            var customerRepo = _unitOfWork.Repository<Customer>();

            var product = new Product
            {
                Id = 1,
                Name = "Test Product",
                PartCode = "TEST-001",
                IsActive = true
            };

            var customer = new Customer
            {
                Id = 1,
                FirstName = "John",
                LastName = "Doe",
                IsActive = true
            };

            // Act
            await productRepo.AddAsync(product);
            await customerRepo.AddAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            // Assert
            var savedProduct = await productRepo.GetByIdAsync(1);
            var savedCustomer = await customerRepo.GetByIdAsync(1);

            Assert.NotNull(savedProduct);
            Assert.NotNull(savedCustomer);
            Assert.Equal("Test Product", savedProduct.Name);
            Assert.Equal("John", savedCustomer.FirstName);
        }

        [Fact]
        public void Dispose_ShouldNotThrowException()
        {
            // Act & Assert
            var uow = new LiteDbUnitOfWork(_database);
            uow.Dispose();
        }
    }
}