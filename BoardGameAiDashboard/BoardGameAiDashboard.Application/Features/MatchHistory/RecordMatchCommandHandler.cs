using MediatR;

namespace BoardGameAiDashboard.Application.Features.MatchHistory;

/// <summary>
/// Placeholder handler for recording match results.
/// Planned for Phase 2 (Game CRUD + Match History).
/// </summary>
internal sealed class RecordMatchCommandHandler
    : IRequestHandler<RecordMatchCommand, Guid>
{
    public Task<Guid> Handle(
        RecordMatchCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "Match recording is planned for Phase 2 (Game CRUD + Match History).");
    }
}
