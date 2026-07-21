using MediatR;

namespace BoardGameAiDashboard.Application.Features.Predictions;

/// <summary>
/// Placeholder handler for win rate predictions.
/// Planned for Phase 8 (ML.NET Predictions).
/// </summary>
internal sealed class GetWinRateQueryHandler
    : IRequestHandler<GetWinRateQuery, WinRateDto>
{
    public Task<WinRateDto> Handle(
        GetWinRateQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "Win rate prediction is planned for Phase 8 (ML Predictions).");
    }
}
