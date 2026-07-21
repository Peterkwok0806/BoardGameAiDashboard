using BoardGameAiDashboard.Application.Common.Models;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.Games.Queries.GetGames;

/// <summary>
/// CQRS query to retrieve a paginated list of board games with optional search.
/// </summary>
public sealed record GetGamesQuery : IRequest<PaginatedList<GameDto>>
{
    /// <summary>Page number (1-based). Defaults to 1.</summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>Page size. Defaults to 10.</summary>
    public int PageSize { get; init; } = 10;

    /// <summary>Optional search term to filter games by name.</summary>
    public string? SearchTerm { get; init; }
}
