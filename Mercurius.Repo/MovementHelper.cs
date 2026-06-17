using Mercurius.Common.BusinessModel;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mercurius.Repo
{
    public class MovementHelper
    {
        public static async Task LogMovementAsync(string transactionId, string movementType, bool isOutbound, string productName, int productId, DateTime transactionDate, decimal countBefore, decimal quantity, decimal countAfter, int locationId, IUnitOfWork unitOfWork)
        {
            await LogMovementAsync(new ItemMovement
            {
                MovementType = movementType,
                Direction = isOutbound ? "OutBound" : "InBound",
                CountAfterTransaction = countAfter,
                CountBeforeTransaction = countBefore,
                Quantity = quantity,
                ItemName = productName,
                LocationId = locationId,
                ProductId = productId,
                TransactionDate = transactionDate,
                TransactionId = transactionId
            }, unitOfWork);
        }

        public static async Task LogMovementAsync(ItemMovement movement, IUnitOfWork unitOfWork)
        {
            // Unique index on TransactionId (LiteDbContext.EnsureCoreIndexes) prevents duplicates.
            // If a movement with the same TransactionId was already logged, the insert is a no-op.
            var repo = unitOfWork.Repository<ItemMovement>();
            var existing = await repo.FindAsync(m => m.TransactionId == movement.TransactionId);
            if (!existing.Any())
            {
                await repo.AddAsync(movement);
                await unitOfWork.SaveChangesAsync();
            }
        }

        public static async Task<List<ItemMovement>> GetMovementsUpToDateAsync(DateTime toDate, int productId, int locationId, IUnitOfWork unitOfWork)
        {
            var repo = unitOfWork.Repository<ItemMovement>();
            var results = await repo.FindAsync(x => x.TransactionDate <= toDate && x.ProductId == productId && x.LocationId == locationId);
            return results.OrderByDescending(x => x.TransactionDate).ToList();
        }
    }
}