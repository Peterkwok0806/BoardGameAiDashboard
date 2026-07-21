using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Application.Common.Models;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.Games.Queries.GetGames;

/// <summary>
/// Handles <see cref="GetGamesQuery"/> by querying the games repository
/// with optional name filtering and pagination.
/// </summary>
internal sealed class GetGamesQueryHandler
    : IRequestHandler<GetGamesQuery, PaginatedList<GameDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetGamesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PaginatedList<GameDto>> Handle(
        GetGamesQuery request,
        CancellationToken cancellationToken)
    {
        System.Linq.Expressions.Expression<Func<Domain.Entities.Game, bool>>? filter =
            string.IsNullOrWhiteSpace(request.SearchTerm)
                ? null
                : g => g.Name.Contains(request.SearchTerm);

        var (items, totalCount) = await _unitOfWork.Games.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            filter,
            cancellationToken);

        var dtos = items.Select(g => new GameDto
        {
            Id = g.Id,
            Name = g.Name,
            Description = g.Description,
            MinPlayers = g.MinPlayers,
            MaxPlayers = g.MaxPlayers,
            CreatedAt = g.CreatedAt,
            UpdatedAt = g.UpdatedAt
        }).ToList();

        return new PaginatedList<GameDto>(
            dtos,
            totalCount,
            request.PageNumber,
            request.PageSize);
    }
}
