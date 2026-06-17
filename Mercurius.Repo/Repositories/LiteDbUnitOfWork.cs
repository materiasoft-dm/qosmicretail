using LiteDB;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mercurius.Repo.Repositories
{
    /// <summary>
    /// LiteDB implementation of Unit of Work.
    /// Manages transactions and repository instances for LiteDB.
    /// </summary>
    public class LiteDbUnitOfWork : IUnitOfWork
    {
        private readonly ILiteDatabase _database;
        private readonly Dictionary<Type, object> _repositories = new();
        private bool _disposed;

        public LiteDbUnitOfWork(ILiteDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public IRepository<T> Repository<T>() where T : class
        {
            var type = typeof(T);

            if (!_repositories.ContainsKey(type))
            {
                _repositories[type] = new LiteDbRepository<T>(_database);
            }

            return (IRepository<T>)_repositories[type];
        }

        public ILiteCollection<T> GetCollection<T>() where T : class
        {
            return _database.GetCollection<T>();
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // LiteDB auto-saves; Checkpoint flushes the write-ahead log to disk.
            _database.Checkpoint();
            return Task.CompletedTask;
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // LiteDB transactions are scoped to the calling thread; if a transaction is already
            // active on this thread BeginTrans() returns false and we treat the existing one
            // as an outer scope (commit/rollback there). Throw on the LiteDB-level "no-op"
            // so callers don't believe a fresh transaction started when one didn't.
            if (!_database.BeginTrans())
            {
                throw new InvalidOperationException(
                    "A LiteDB transaction is already active on this thread. Nested transactions are not supported.");
            }
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_database.Commit())
            {
                throw new InvalidOperationException(
                    "No active LiteDB transaction to commit on this thread. Call BeginTransactionAsync first.");
            }
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_database.Rollback())
            {
                throw new InvalidOperationException(
                    "No active LiteDB transaction to roll back on this thread. Call BeginTransactionAsync first.");
            }
            return Task.CompletedTask;
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
                    // _database is owned by the singleton LiteDbContext; do not dispose it here.
                    _repositories.Clear();
                }
                _disposed = true;
            }
        }
    }
}
