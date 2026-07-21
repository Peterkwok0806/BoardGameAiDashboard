using MediatR;

namespace BoardGameAiDashboard.Application.Features.Predictions;

/// <summary>
/// Placeholder handler for churn predictions.
/// Planned for Phase 8 (ML.NET Predictions).
/// </summary>
internal sealed class GetChurnPredictionQueryHandler
    : IRequestHandler<GetChurnPredictionQuery, ChurnPredictionDto>
{
    public Task<ChurnPredictionDto> Handle(
        GetChurnPredictionQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "Churn prediction is planned for Phase 8 (ML Predictions).");
    }
}
