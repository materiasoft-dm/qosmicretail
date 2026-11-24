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

        /// <summary>
        /// Forgets every change not yet saved. After a SaveChanges fails (a concurrency conflict, a
        /// constraint violation) the failed entities stay tracked, and the next SaveChanges on this
        /// unit of work would re-attempt them — so a caller that carries on after catching the
        /// exception must call this first. A rolled-back transaction does it for you.
        /// </summary>
        void DiscardChanges();

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
}
