using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;

namespace Mercurius.Repo.Repositories
{
    /// <summary>
    /// EF Core (SQLite) implementation of Unit of Work. Replaces LiteDbUnitOfWork.
    /// Unlike LiteDB (which auto-persists every repository call), nothing hits the
    /// database until SaveChangesAsync is called.
    /// </summary>
    public class EfUnitOfWork : IUnitOfWork
    {
        private readonly MercuriusDbContext _context;
        private readonly ICurrentTenantContext _currentTenantContext;
        private readonly Dictionary<Type, object> _repositories = new();
        private IDbContextTransaction? _currentTransaction;
        private bool _disposed;

        public EfUnitOfWork(MercuriusDbContext context, ICurrentTenantContext currentTenantContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currentTenantContext = currentTenantContext;
        }

        public IRepository<T> Repository<T>() where T : class
        {
            var type = typeof(T);

            if (!_repositories.ContainsKey(type))
            {
                _repositories[type] = new EfRepository<T>(_context, _currentTenantContext);
            }

            return (IRepository<T>)_repositories[type];
        }

        public IQueryable<T> Query<T>() where T : class
        {
            return _context.Set<T>();
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction != null)
            {
                throw new InvalidOperationException(
                    "A transaction is already active on this unit of work. Nested transactions are not supported.");
            }
            _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction == null)
            {
                throw new InvalidOperationException(
                    "No active transaction to commit. Call BeginTransactionAsync first.");
            }
            await _context.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction == null)
            {
                throw new InvalidOperationException(
                    "No active transaction to roll back. Call BeginTransactionAsync first.");
            }
            await _currentTransaction.RollbackAsync(cancellationToken);
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // _context is owned by the DI container (scoped); do not dispose it here.
                    _currentTransaction?.Dispose();
                    _repositories.Clear();
                }
                _disposed = true;
            }
        }
    }
}
