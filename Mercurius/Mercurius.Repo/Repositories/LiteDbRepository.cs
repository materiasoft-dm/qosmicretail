using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Mercurius.Repo.Repositories
{
    /// <summary>
    /// LiteDB implementation of the generic repository.
    /// The LiteDB driver is synchronous. All async methods return completed tasks
    /// to satisfy the IRepository&lt;T&gt; contract, which exists so a future SQL/Mongo
    /// implementation can be genuinely async without changing callers.
    /// CancellationToken is honoured via ThrowIfCancellationRequested before each operation.
    /// </summary>
    public class LiteDbRepository<T> : IRepository<T> where T : class
    {
        private readonly ILiteDatabase _database;
        private readonly string _collectionName;
        private ILiteCollection<T> Collection => _database.GetCollection<T>(_collectionName);

        public LiteDbRepository(ILiteDatabase database, string? collectionName = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _collectionName = collectionName ?? typeof(T).Name;
        }

        public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Collection.FindById(id));
        }

        public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Collection.FindById(id));
        }

        public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<T>>(Collection.FindAll().ToList());
        }

        public Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<T>>(Collection.Find(predicate).ToList());
        }

        public Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Collection.Insert(entity);
            return Task.FromResult(entity);
        }

        public Task<IReadOnlyList<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entityList = entities.ToList();
            Collection.InsertBulk(entityList);
            return Task.FromResult<IReadOnlyList<T>>(entityList);
        }

        public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Collection.Update(entity);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Collection.Delete(id);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Collection.Delete(id);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Collection.Exists(Query.EQ("_id", id)));
        }

        public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Collection.Exists(Query.EQ("_id", id)));
        }

        public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (predicate == null)
                return Task.FromResult(Collection.Count());

            return Task.FromResult(Collection.Count(predicate));
        }

        public Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            int pageNumber = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 1;
            var skip = (pageNumber - 1) * pageSize;

            var query = Collection.Query();
            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var totalCount = query.Count();

            List<T> pagedItems;
            if (orderBy != null)
            {
                // Caller-provided ordering uses LINQ-to-objects; fall back to client-side paging
                // for the ordered slice. Predicate is still applied server-side above.
                IQueryable<T> ordered = orderBy(query.ToEnumerable().AsQueryable());
                pagedItems = ordered.Skip(skip).Take(pageSize).ToList();
            }
            else
            {
                pagedItems = query.Skip(skip).Limit(pageSize).ToList();
            }

            return Task.FromResult(((IReadOnlyList<T>)pagedItems, totalCount));
        }

        // LiteDB-specific methods for creating indexes
        public void EnsureIndex<K>(Expression<Func<T, K>> property, bool unique = false)
        {
            Collection.EnsureIndex(property, unique);
        }

        public void EnsureIndex(string fieldName, bool unique = false)
        {
            Collection.EnsureIndex(fieldName, unique);
        }
    }
}
