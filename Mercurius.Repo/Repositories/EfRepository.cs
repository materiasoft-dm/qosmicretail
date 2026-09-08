using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Mercurius.Repo.Repositories
{
    /// <summary>
    /// EF Core (SQLite) implementation of the generic repository. Replaces LiteDbRepository&lt;T&gt;.
    /// Unlike LiteDB, nothing is persisted until IUnitOfWork.SaveChangesAsync is called.
    /// </summary>
    public class EfRepository<T> : IRepository<T> where T : class
    {
        // Nothing is ever hard-deleted for an entity that has an IsActive flag — Delete just
        // flips it off. Resolved once per T since reflection lookups aren't free.
        private static readonly PropertyInfo? IsActiveProperty =
            typeof(T).GetProperty("IsActive", typeof(bool));

        private readonly MercuriusDbContext _context;
        private readonly DbSet<T> _dbSet;

        public EfRepository(MercuriusDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = _context.Set<T>();
        }

        public async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _dbSet.ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
        }

        public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            await _dbSet.AddAsync(entity, cancellationToken);
            return entity;
        }

        public async Task<IReadOnlyList<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            var entityList = entities.ToList();
            await _dbSet.AddRangeAsync(entityList, cancellationToken);
            return entityList;
        }

        public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Handles both a freshly-bound (untracked) entity and an already-tracked one
            // fetched earlier in the same request — EF Core no-ops the attach for the latter.
            if (_context.Entry(entity).State == EntityState.Detached)
            {
                _dbSet.Attach(entity);
            }
            _context.Entry(entity).State = EntityState.Modified;
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
            if (entity != null)
            {
                RemoveOrDeactivate(entity);
            }
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var entity = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
            if (entity != null)
            {
                RemoveOrDeactivate(entity);
            }
        }

        /// <summary>
        /// Soft-deletes (IsActive = false) when the entity has that flag; only falls back to an
        /// actual row delete for the few entities with no concept of "active" (join tables,
        /// audit logs, etc.).
        /// </summary>
        private void RemoveOrDeactivate(T entity)
        {
            if (IsActiveProperty != null)
            {
                IsActiveProperty.SetValue(entity, false);
                _context.Entry(entity).State = EntityState.Modified;
            }
            else
            {
                _dbSet.Remove(entity);
            }
        }

        public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        {
            return await GetByIdAsync(id, cancellationToken) != null;
        }

        public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
        {
            return await GetByIdAsync(id, cancellationToken) != null;
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            return predicate == null
                ? await _dbSet.CountAsync(cancellationToken)
                : await _dbSet.CountAsync(predicate, cancellationToken);
        }

        public async Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            int pageNumber = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 1;
            var skip = (pageNumber - 1) * pageSize;

            IQueryable<T> query = _dbSet;
            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            // Ordering (when supplied) now translates fully to SQL, unlike the LiteDB version,
            // which had to materialize the filtered set in memory first.
            if (orderBy != null)
            {
                query = orderBy(query);
            }

            var pagedItems = await query.Skip(skip).Take(pageSize).ToListAsync(cancellationToken);
            return (pagedItems, totalCount);
        }
    }
}
