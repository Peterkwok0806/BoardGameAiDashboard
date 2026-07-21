using BoardGameAiDashboard.Application.Common.Interfaces;
using BoardGameAiDashboard.Domain.Entities;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.Games.Commands.CreateGame;

/// <summary>
/// Handles <see cref="CreateGameCommand"/> by persisting a new <see cref="Game"/> entity.
/// </summary>
internal sealed class CreateGameCommandHandler
    : IRequestHandler<CreateGameCommand, CreateGameCommandResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateGameCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateGameCommandResponse> Handle(
        CreateGameCommand request,
        CancellationToken cancellationToken)
    {
        var game = new Game(
            request.Name,
            request.Description,
            request.MinPlayers,
            request.MaxPlayers);

        await _unitOfWork.Games.AddAsync(game, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateGameCommandResponse
        {
            Id = game.Id,
            Name = game.Name,
            Description = game.Description,
            MinPlayers = game.MinPlayers,
            MaxPlayers = game.MaxPlayers,
            CreatedAt = game.CreatedAt
        };
    }
}
