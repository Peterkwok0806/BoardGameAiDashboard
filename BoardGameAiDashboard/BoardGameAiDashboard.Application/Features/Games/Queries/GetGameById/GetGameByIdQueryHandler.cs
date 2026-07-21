using BoardGameAiDashboard.Application.Common.Exceptions;
using BoardGameAiDashboard.Application.Common.Interfaces;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.Games.Queries.GetGameById;

/// <summary>
/// Handles <see cref="GetGameByIdQuery"/> by fetching a single game with navigation counts.
/// Throws <see cref="NotFoundException"/> if the game does not exist.
/// </summary>
internal sealed class GetGameByIdQueryHandler
    : IRequestHandler<GetGameByIdQuery, GameDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetGameByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GameDetailDto> Handle(
        GetGameByIdQuery request,
        CancellationToken cancellationToken)
    {
        var game = await _unitOfWork.Games.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Game), request.Id);

        return new GameDetailDto
        {
            Id = game.Id,
            Name = game.Name,
            Description = game.Description,
            MinPlayers = game.MinPlayers,
            MaxPlayers = game.MaxPlayers,
            CreatedAt = game.CreatedAt,
            UpdatedAt = game.UpdatedAt,
            RuleChunkCount = game.RuleChunks.Count,
            CharacterCount = game.Characters.Count,
            CardCount = game.Cards.Count,
            MatchHistoryCount = game.MatchHistories.Count
        };
    }
}
