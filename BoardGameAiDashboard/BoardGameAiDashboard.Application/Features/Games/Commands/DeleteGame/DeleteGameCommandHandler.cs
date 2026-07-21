using BoardGameAiDashboard.Application.Common.Exceptions;
using BoardGameAiDashboard.Application.Common.Interfaces;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.Games.Commands.DeleteGame;

/// <summary>
/// Handles <see cref="DeleteGameCommand"/> by soft-deleting an existing game.
/// Throws <see cref="NotFoundException"/> if the game does not exist.
/// </summary>
internal sealed class DeleteGameCommandHandler
    : IRequestHandler<DeleteGameCommand, Unit>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteGameCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(
        DeleteGameCommand request,
        CancellationToken cancellationToken)
    {
        var game = await _unitOfWork.Games.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Game), request.Id);

        game.SoftDelete();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
