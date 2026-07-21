using MediatR;

namespace BoardGameAiDashboard.Application.Features.Predictions;

/// <summary>
/// CQRS query to get churn risk prediction for a specific user.
/// Planned for Phase 8 (ML Predictions).
/// </summary>
public sealed record GetChurnPredictionQuery : IRequest<ChurnPredictionDto>
{
    /// <summary>The user identifier.</summary>
    public Guid UserId { get; init; }
}

/// <summary>DTO for churn prediction results.</summary>
public sealed record ChurnPredictionDto
{
    /// <summary>Churn risk score (0-1, higher = more likely to churn).</summary>
    public double ChurnRisk { get; init; }

    /// <summary>Risk category: Low, Medium, High.</summary>
    public string RiskLevel { get; init; } = string.Empty;

    /// <summary>Days since last active session.</summary>
    public int DaysSinceLastActive { get; init; }
}
