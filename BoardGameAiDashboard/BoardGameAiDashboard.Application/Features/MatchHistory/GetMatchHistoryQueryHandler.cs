using MediatR;

namespace BoardGameAiDashboard.Application.Features.MatchHistory;

/// <summary>
/// Placeholder handler for retrieving match history.
/// Planned for Phase 2 (Game CRUD + Match History).
/// </summary>
internal sealed class GetMatchHistoryQueryHandler
    : IRequestHandler<GetMatchHistoryQuery, List<MatchHistoryDto>>
{
    public Task<List<MatchHistoryDto>> Handle(
        GetMatchHistoryQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "Match history query is planned for Phase 2 (Game CRUD + Match History).");
    }
}
