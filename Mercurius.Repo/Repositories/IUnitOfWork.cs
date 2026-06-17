using System;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;

namespace Mercurius.Repo.Repositories
{
    /// <summary>
    /// Unit of Work pattern for managing transactions across multiple repositories.
    /// This ensures data consistency when operations span multiple entities.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        IRepository<T> Repository<T>() where T : class;

        /// <summary>
        /// Gets the underlying LiteDB collection for direct query access.
        /// Use this for complex queries (filtering, sorting, pagination) that the
        /// generic repository doesn't support directly.
        /// </summary>
        ILiteCollection<T> GetCollection<T>() where T : class;

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
}
