using System.Linq.Expressions;
using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Domain.Common;
using BoardGameAiDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BoardGameAiDashboard.Infrastructure.Common.Repositories;

/// <summary>
/// Generic repository implementation using EF Core.
/// Soft-delete is handled automatically by EF Core HasQueryFilter on the DbContext.
/// </summary>
/// <typeparam name="T">The entity type (must derive from BaseEntity).</typeparam>
public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    protected readonly ApplicationDbContext _context;

    public GenericRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<T>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<T>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _context.Set<T>().AsNoTracking();

        if (filter is not null)
        {
            query = query.Where(filter);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Set<T>().AddAsync(entity, cancellationToken);
        return entry.Entity;
    }

    /// <inheritdoc />
    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _context.Set<T>().Attach(entity);
        _context.Entry(entity).State = EntityState.Modified;
        entity.MarkUpdated();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Set<T>()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is not null)
        {
            entity.SoftDelete();

            _context.Set<T>().Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
        }
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _context.Set<T>().AsNoTracking();

        if (filter is not null)
        {
            query = query.Where(filter);
        }

        return await query.CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> FindOneAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<T>()
            .AsNoTracking()
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public IQueryable<T> Query()
    {
        return _context.Set<T>().AsQueryable();
    }
}
