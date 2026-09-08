using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        /// Gets a queryable for direct query access (filtering, sorting, pagination) that the
        /// generic repository doesn't support directly. Backed by the underlying DbSet&lt;T&gt;.
        /// </summary>
        IQueryable<T> Query<T>() where T : class;

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
}
