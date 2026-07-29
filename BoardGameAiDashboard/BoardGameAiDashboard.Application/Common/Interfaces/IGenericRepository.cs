using System.Linq.Expressions;
using BoardGameAiDashboard.Domain.Common;

namespace BoardGameAiDashboard.Application.Common.Interfaces;

/// <summary>
/// Generic repository abstraction for CRUD operations.
/// Implementations live in the Infrastructure layer (EF Core).
/// </summary>
/// <typeparam name="T">The entity type (must derive from BaseEntity).</typeparam>
public interface IGenericRepository<T> where T : BaseEntity
{
    /// <summary>Get an entity by its primary key (Guid Id).</summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Get all non-deleted entities.</summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a paged subset of entities with an optional filter.
    /// Returns the items and the total count (before pagination).
    /// </summary>
    Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>Add a new entity and return it.</summary>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>Update an existing entity (caller is responsible for setting properties).</summary>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete an entity by setting IsDeleted = true.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Count entities matching an optional filter.</summary>
    Task<int> CountAsync(
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find the first entity matching the given predicate, or null if none matches.
    /// </summary>
    Task<T?> FindOneAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Expose an <see cref="IQueryable{T}"/> for advanced LINQ queries.
    /// The caller is responsible for applying AsNoTracking() and soft-delete filters
    /// unless the implementation handles them automatically.
    /// </summary>
    IQueryable<T> Query();
}
