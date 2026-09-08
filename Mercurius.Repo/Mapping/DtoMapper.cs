using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Mercurius.Repo.Mapping
{
    /// <summary>
    /// Generic reflection-based mapping between an EF entity (Mercurius.Repo.Models.*) and its
    /// paired DTO (Mercurius.Repo.Dtos.*Dto). Works uniformly across every entity because DTOs
    /// are, by convention, the entity's scalar properties only (navigation properties omitted),
    /// so every matching pair shares identical property names/types.
    /// </summary>
    public static class DtoMapper
    {
        private static readonly ConcurrentDictionary<(Type Entity, Type Dto), object> ProjectionCache = new();
        private static readonly ConcurrentDictionary<(Type, Type), PropertyInfo[]> MatchedPropertiesCache = new();

        /// <summary>
        /// Cached Entity -> Dto projection expression, e.g. for use in EF Core .Select(...).
        /// Built once per (TEntity, TDto) pair and reused — this is what lets subsequent
        /// Where/OrderBy/Skip/Take compose against the projected IQueryable&lt;TDto&gt; and still
        /// translate to SQL.
        /// </summary>
        public static Expression<Func<TEntity, TDto>> Projection<TEntity, TDto>()
        {
            return (Expression<Func<TEntity, TDto>>)ProjectionCache.GetOrAdd((typeof(TEntity), typeof(TDto)), _ => BuildProjection<TEntity, TDto>());
        }

        private static Expression<Func<TEntity, TDto>> BuildProjection<TEntity, TDto>()
        {
            var param = Expression.Parameter(typeof(TEntity), "e");
            var bindings = MatchedProperties(typeof(TEntity), typeof(TDto))
                .Select(dtoProp =>
                {
                    var entityProp = typeof(TEntity).GetProperty(dtoProp.Name)!;
                    return (MemberBinding)Expression.Bind(dtoProp, Expression.Property(param, entityProp));
                });
            var body = Expression.MemberInit(Expression.New(typeof(TDto)), bindings);
            return Expression.Lambda<Func<TEntity, TDto>>(body, param);
        }

        /// <summary>Single-object Entity -> Dto copy (for paths that already have a materialized entity).</summary>
        public static TDto ToDto<TEntity, TDto>(TEntity entity) where TDto : new()
        {
            var dto = new TDto();
            foreach (var dtoProp in MatchedProperties(typeof(TEntity), typeof(TDto)))
            {
                var entityProp = typeof(TEntity).GetProperty(dtoProp.Name)!;
                dtoProp.SetValue(dto, entityProp.GetValue(entity));
            }
            return dto;
        }

        /// <summary>Builds a new entity instance populated from a Dto's scalar properties.</summary>
        public static TEntity ToEntity<TDto, TEntity>(TDto dto) where TEntity : new()
        {
            var entity = new TEntity();
            CopyToEntity(dto, entity);
            return entity;
        }

        /// <summary>Copies a Dto's scalar properties onto an existing entity instance.</summary>
        public static void CopyToEntity<TDto, TEntity>(TDto dto, TEntity entity)
        {
            foreach (var dtoProp in MatchedProperties(typeof(TEntity), typeof(TDto)))
            {
                var entityProp = typeof(TEntity).GetProperty(dtoProp.Name)!;
                if (!entityProp.CanWrite) continue;
                entityProp.SetValue(entity, dtoProp.GetValue(dto));
            }
        }

        /// <summary>Resolves the Mercurius.Repo.Models.* entity type paired with a DTO by naming convention (FooDto -> Foo).</summary>
        public static Type ResolveEntityType(Type dtoType)
        {
            var entityName = dtoType.Name.EndsWith("Dto", StringComparison.Ordinal)
                ? dtoType.Name[..^3]
                : dtoType.Name;
            var entityTypeName = $"Mercurius.Repo.Models.{entityName}";
            var entityType = dtoType.Assembly.GetType(entityTypeName);
            if (entityType == null)
            {
                throw new InvalidOperationException($"No entity type '{entityTypeName}' found for DTO '{dtoType.FullName}'. DTO classes must be named '<Entity>Dto'.");
            }
            return entityType;
        }

        private static PropertyInfo[] MatchedProperties(Type entityType, Type dtoType)
        {
            return MatchedPropertiesCache.GetOrAdd((entityType, dtoType), _ =>
                dtoType.GetProperties()
                    .Where(dtoProp =>
                    {
                        var entityProp = entityType.GetProperty(dtoProp.Name);
                        return entityProp != null && entityProp.PropertyType == dtoProp.PropertyType && dtoProp.CanWrite;
                    })
                    .ToArray());
        }
    }
}
