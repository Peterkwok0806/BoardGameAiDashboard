using BoardGameAiDashboard.Application.Common.Exceptions;
using BoardGameAiDashboard.Application.Common.Interfaces;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.Games.Commands.UpdateGame;

/// <summary>
/// Handles <see cref="UpdateGameCommand"/> by updating an existing <see cref="Domain.Entities.Game"/> entity.
/// Throws <see cref="NotFoundException"/> if the game does not exist.
/// </summary>
internal sealed class UpdateGameCommandHandler
    : IRequestHandler<UpdateGameCommand, UpdateGameCommandResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateGameCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateGameCommandResponse> Handle(
        UpdateGameCommand request,
        CancellationToken cancellationToken)
    {
        var game = await _unitOfWork.Games.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Game), request.Id);

        game.Update(
            request.Name,
            request.Description,
            request.MinPlayers,
            request.MaxPlayers);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateGameCommandResponse
        {
            Id = game.Id,
            Name = game.Name,
            Description = game.Description,
            MinPlayers = game.MinPlayers,
            MaxPlayers = game.MaxPlayers,
            UpdatedAt = game.UpdatedAt
        };
    }
}
