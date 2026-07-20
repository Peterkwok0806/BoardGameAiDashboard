namespace BoardGameAiDashboard.Application.Common.Models;

/// <summary>
/// A read-only, paginated collection.
/// Does NOT inherit from <see cref="List{T}"/> — exposes an <see cref="Items"/> property instead.
/// The actual query execution (EF Core) is handled by the caller (repository / handler).
/// </summary>
public class PaginatedList<T>
{
    /// <summary>The page of items.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>Total number of items across all pages.</summary>
    public int TotalCount { get; }

    /// <summary>Current page number (1-based).</summary>
    public int PageNumber { get; }

    /// <summary>Requested page size.</summary>
    public int PageSize { get; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages { get; }

    /// <summary>
    /// Creates a new paginated list from pre-fetched data.
    /// </summary>
    /// <param name="items">The items for the current page.</param>
    /// <param name="totalCount">Total count of items across all pages.</param>
    /// <param name="pageNumber">Current page number (1-based).</param>
    /// <param name="pageSize">Requested page size.</param>
    public PaginatedList(
        IReadOnlyList<T> items,
        int totalCount,
        int pageNumber,
        int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}
